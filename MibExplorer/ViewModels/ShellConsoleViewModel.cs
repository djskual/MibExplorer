using Microsoft.Win32;
using System.Windows;
using System.IO;
using System.Windows.Documents;
using System.Windows.Media;
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
    private FlowDocument? _document;
    private Paragraph? _currentParagraph;

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

    public void AttachDocument(FlowDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _document.PagePadding = new Thickness(0);
        _document.Blocks.Clear();
        _currentParagraph = null;
    }

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

    public async Task SendCommandDirectAsync(string command)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(command))
            return;

        if (_shellSession is null || !_shellSession.IsOpen || !IsConnected)
            return;

        try
        {
            await _shellSession.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            AppendOutputLine($"[shell error] {ex.Message}");
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

        AppendFormattedText(text);
    }

    private void AppendOutputLine(string line)
    {
        string updated = OutputText + line + Environment.NewLine;
        OutputText = TrimOutputIfNeeded(updated);

        AppendFormattedSystemLine(line + Environment.NewLine);
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
        _document?.Blocks.Clear();
        _currentParagraph = null;
    }

    private void AppendFormattedText(string text)
    {
        if (_document is null || string.IsNullOrEmpty(text))
            return;

        string normalized = text.Replace("\r\n", "\n");

        int start = 0;

        for (int i = 0; i < normalized.Length; i++)
        {
            if (normalized[i] != '\n')
                continue;

            string segment = normalized.Substring(start, i - start);
            AppendFormattedSegment(segment, endLine: true);
            start = i + 1;
        }

        if (start < normalized.Length)
        {
            string segment = normalized[start..];
            AppendFormattedSegment(segment, endLine: false);
        }

        TrimDocumentIfNeeded();
    }

    private void AppendFormattedSystemLine(string line)
    {
        if (_document is null)
            return;

        _currentParagraph = null;

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 16
        };

        var run = new Run(line)
        {
            Foreground = GetBrushForSystemLine(line),
            FontStyle = FontStyles.Italic
        };

        paragraph.Inlines.Add(run);
        _document.Blocks.Add(paragraph);

        TrimDocumentIfNeeded();
    }

    private void AppendFormattedSegment(string text, bool endLine)
    {
        if (_document is null)
            return;

        if (_currentParagraph is null)
        {
            _currentParagraph = new Paragraph
            {
                Margin = new Thickness(0),
                LineHeight = 16
            };

            _document.Blocks.Add(_currentParagraph);
        }

        if (!string.IsNullOrEmpty(text))
        {
            if (TryAppendPromptSegments(text))
                return;

            var run = new Run(text)
            {
                Foreground = GetBrushForPromptOrOutput(text)
            };

            _currentParagraph.Inlines.Add(run);
        }

        if (endLine)
        {
            _currentParagraph = null;
        }
    }

    private bool TryAppendPromptSegments(string text)
    {
        if (_currentParagraph is null)
            return false;

        string trimmed = text.TrimEnd();

        if (!IsPromptText(trimmed))
            return false;

        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        string left = trimmed.Substring(0, colonIndex + 1);
        string right = trimmed.Substring(colonIndex + 1);

        string path = right.TrimEnd('>', '#', '$');
        string symbol = right.Substring(path.Length);

        var brushUser = new SolidColorBrush(Color.FromRgb(110, 210, 255));
        var brushPath = new SolidColorBrush(Color.FromRgb(160, 200, 255));

        _currentParagraph.Inlines.Add(new Run(left)
        {
            Foreground = brushUser
        });

        _currentParagraph.Inlines.Add(new Run(path)
        {
            Foreground = brushPath
        });

        _currentParagraph.Inlines.Add(new Run(symbol + " ")
        {
            Foreground = brushUser
        });

        return true;
    }

    private Brush GetBrushForPromptOrOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new SolidColorBrush(Color.FromRgb(230, 230, 230));

        string lower = text.ToLowerInvariant();

        if (lower.Contains("error") ||
            lower.Contains("not found") ||
            lower.Contains("failed") ||
            lower.Contains("cannot execute") ||
            lower.Contains("no such file") ||
            lower.Contains("permission denied"))
        {
            return new SolidColorBrush(Color.FromRgb(255, 110, 90));
        }

        if (lower.Contains("warning"))
            return new SolidColorBrush(Color.FromRgb(255, 196, 96));

        if (IsPromptText(text))
            return new SolidColorBrush(Color.FromRgb(110, 210, 255));

        return new SolidColorBrush(Color.FromRgb(210, 210, 210));
    }

    private Brush GetBrushForSystemLine(string line)
    {
        string lower = line.ToLowerInvariant();

        if (lower.StartsWith("[shell] output trimmed"))
            return new SolidColorBrush(Color.FromRgb(255, 196, 96));

        if (lower.StartsWith("[shell]"))
            return new SolidColorBrush(Color.FromRgb(140, 190, 255));

        return new SolidColorBrush(Color.FromRgb(220, 220, 220));
    }

    private static bool IsPromptText(string text)
    {
        string trimmed = text.TrimEnd();

        if (!trimmed.Contains("@") || !trimmed.Contains(":"))
            return false;

        return trimmed.EndsWith(">") || trimmed.EndsWith("#") || trimmed.EndsWith("$");
    }

    private void TrimDocumentIfNeeded()
    {
        if (_document is null)
            return;

        const int maxBlocks = 2000;

        while (_document.Blocks.Count > maxBlocks && _document.Blocks.FirstBlock is not null)
        {
            _document.Blocks.Remove(_document.Blocks.FirstBlock);
        }
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