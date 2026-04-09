using Microsoft.Win32;
using System.Windows;
using System.IO;
using MibExplorer.Core;
using MibExplorer.Services;

namespace MibExplorer.ViewModels;

public sealed class ShellConsoleViewModel : ObservableObject, IDisposable
{
    private const int MaxOutputLength = 80000;
    private const int TrimToLength = 60000;

    private readonly IMibConnectionService _connectionService;
    private IRemoteShellSession? _shellSession;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _disposed;

    private string _outputText = string.Empty;
    private string _currentCommand = string.Empty;
    private bool _isConnected;
    private bool _isBusy;
    private string _statusText = "Shell not started.";

    public ShellConsoleViewModel(IMibConnectionService connectionService)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _connectionService.ConnectionStateChanged += ConnectionService_ConnectionStateChanged;

        ClearCommand = new RelayCommand(ClearOutput);
        CopyAllCommand = new RelayCommand(CopyAllOutput, () => !string.IsNullOrEmpty(OutputText));
        SaveLogCommand = new RelayCommand(SaveLog, () => !string.IsNullOrEmpty(OutputText));
        SendCommand = new RelayCommand(
            async () => await SendCurrentCommandAsync(),
            () => CanSendCurrentCommand());
    }

    public string OutputText
    {
        get => _outputText;
        set
        {
            if (SetProperty(ref _outputText, value))
            {
                CopyAllCommand.RaiseCanExecuteChanged();
                SaveLogCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CurrentCommand
    {
        get => _currentCommand;
        set => SetProperty(ref _currentCommand, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(CanEditCommand));
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanEditCommand));
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool CanEditCommand => IsConnected && !IsBusy;

    public RelayCommand ClearCommand { get; }

    public RelayCommand CopyAllCommand { get; }

    public RelayCommand SaveLogCommand { get; }

    public RelayCommand SendCommand { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_connectionService.IsConnected)
        {
            IsConnected = false;
            StatusText = "SSH connection unavailable.";
            AppendOutputLine("[shell] SSH connection unavailable.");
            return;
        }

        try
        {
            _shellSession = await _connectionService.CreateShellSessionAsync(cancellationToken);
            _shellSession.TextReceived += OnShellTextReceived;
            _shellSession.Closed += OnShellClosed;

            await _shellSession.StartAsync(cancellationToken);

            IsConnected = _shellSession.IsOpen;
            StatusText = IsConnected ? "Shell connected." : "Shell unavailable.";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = "Failed to start shell.";
            AppendOutputLine($"[shell error] {ex.Message}");
        }
    }

    public async Task SendCurrentCommandAsync()
    {
        ThrowIfDisposed();

        if (!CanSendCurrentCommand())
            return;

        string command = CurrentCommand.TrimEnd();
        if (string.IsNullOrWhiteSpace(command))
            return;

        try
        {
            IsBusy = true;

            AddToHistory(command);

            CurrentCommand = string.Empty;

            if (_shellSession is null || !_shellSession.IsOpen)
                throw new InvalidOperationException("Shell session is not available.");

            await _shellSession.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            AppendOutputLine($"[shell error] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void BrowseHistoryUp()
    {
        if (_history.Count == 0)
            return;

        if (_historyIndex < 0)
            _historyIndex = _history.Count - 1;
        else if (_historyIndex > 0)
            _historyIndex--;

        CurrentCommand = _history[_historyIndex];
    }

    public void BrowseHistoryDown()
    {
        if (_history.Count == 0)
            return;

        if (_historyIndex < 0)
            return;

        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            CurrentCommand = _history[_historyIndex];
        }
        else
        {
            _historyIndex = -1;
            CurrentCommand = string.Empty;
        }
    }

    private bool CanSendCurrentCommand()
    {
        return IsConnected && !IsBusy;
    }

    private void AddToHistory(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        if (_history.Count == 0 || !string.Equals(_history[^1], command, StringComparison.Ordinal))
            _history.Add(command);

        _historyIndex = -1;
    }

    private static string NormalizeShellText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string normalized = text
            .Replace("\r\r\n", "\n")
            .Replace("\r\n", "\n")
            .Replace("\n\r", "\n")
            .Replace("\r", "\n")
            .Replace("\n\n\n", "\n\n");

        return normalized.Replace("\n", Environment.NewLine);
    }

    private void OnShellTextReceived(object? sender, string text)
    {
        string normalized = NormalizeShellText(text);

        if (Application.Current.Dispatcher.CheckAccess())
        {
            AppendOutput(normalized);
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() => AppendOutput(normalized));
        }
    }

    private void OnShellClosed(object? sender, EventArgs e)
    {
        void CloseUi()
        {
            IsConnected = false;
            StatusText = "Shell closed.";
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            CloseUi();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(CloseUi);
        }
    }

    private void ConnectionService_ConnectionStateChanged(object? sender, bool isConnected)
    {
        if (isConnected)
            return;

        void CloseUi()
        {
            if (!IsConnected && _shellSession is null)
                return;

            IsConnected = false;
            IsBusy = false;
            StatusText = "Shell closed.";
            AppendOutputLine("[shell] Connection lost.");
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            CloseUi();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(CloseUi);
        }
    }

    private void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        string updated = OutputText + text;
        OutputText = TrimOutputIfNeeded(updated);
    }

    private void AppendOutputLine(string line)
    {
        string updated = OutputText + line + Environment.NewLine;
        OutputText = TrimOutputIfNeeded(updated);
    }

    private static string TrimOutputIfNeeded(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= MaxOutputLength)
            return text;

        string trimmed = text[^TrimToLength..];

        int firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine >= 0 && firstNewLine < trimmed.Length - 1)
            trimmed = trimmed[(firstNewLine + 1)..];

        return "[shell] Output trimmed." + Environment.NewLine + trimmed;
    }

    private void ClearOutput()
    {
        OutputText = string.Empty;
    }

    private void CopyAllOutput()
    {
        if (string.IsNullOrEmpty(OutputText))
            return;

        Clipboard.SetText(OutputText);
    }

    private void SaveLog()
    {
        if (string.IsNullOrEmpty(OutputText))
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Save shell log",
            Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = $"mibexplorer-shell-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() != true)
            return;

        File.WriteAllText(dialog.FileName, OutputText);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _connectionService.ConnectionStateChanged -= ConnectionService_ConnectionStateChanged;

        if (_shellSession is not null)
        {
            _shellSession.TextReceived -= OnShellTextReceived;
            _shellSession.Closed -= OnShellClosed;
            _shellSession.Dispose();
            _shellSession = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ShellConsoleViewModel));
    }
}