using MibExplorer.Core;
using MibExplorer.Services;
using System.Windows;

namespace MibExplorer.ViewModels;

// The terminal emulator owns display, editing and scrollback. This VM owns
// only the SSH PTY session and the connection state.
public sealed class ShellConsoleViewModel : ObservableObject, IDisposable
{
    private readonly IMibConnectionService _connectionService;
    private IRemoteShellSession? _shellSession;
    private bool _disposed;
    private readonly List<string> _pendingCommands = new();
    private bool _isConnected;
    private string _statusText = "Terminal starting...";

    public ShellConsoleViewModel(IMibConnectionService connectionService)
    {
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _connectionService.ConnectionStateChanged += ConnectionService_ConnectionStateChanged;
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public event EventHandler<string>? OutputReceived;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_connectionService.IsConnected)
        {
            StatusText = "SSH connection unavailable.";
            return;
        }

        try
        {
            _shellSession = await _connectionService.CreateShellSessionAsync(cancellationToken);
            _shellSession.TextReceived += OnTextReceived;
            _shellSession.Closed += OnShellClosed;
            await _shellSession.StartAsync(cancellationToken);

            IsConnected = _shellSession.IsOpen;
            StatusText = IsConnected ? "Shell connected." : "Shell unavailable.";
            if (IsConnected)
            {
                foreach (string command in _pendingCommands)
                    await _shellSession.SendCommandAsync(command, cancellationToken);
                _pendingCommands.Clear();
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = $"Failed to start shell: {ex.Message}";
        }
    }

    // Used by "Open Shell Here" in MainWindow. Queue until WebView2/SSH PTY
    // initialization has completed rather than relying on an arbitrary timer.
    public async Task SendCommandDirectAsync(string command)
    {
        ThrowIfDisposed();
        if (!_connectionService.IsConnected) return;
        if (_shellSession?.IsOpen != true)
        {
            _pendingCommands.Add(command);
            return;
        }
        await _shellSession.SendCommandAsync(command);
    }

    public Task WriteInputAsync(string input, CancellationToken cancellationToken = default) =>
        _shellSession?.WriteInputAsync(input, cancellationToken) ?? Task.CompletedTask;

    public Task WriteBytesAsync(byte[] data, CancellationToken cancellationToken = default) =>
        _shellSession?.WriteBytesAsync(data, cancellationToken) ?? Task.CompletedTask;

    public Task ResizeAsync(uint columns, uint rows, CancellationToken cancellationToken = default) =>
        _shellSession?.ResizeAsync(columns, rows, cancellationToken) ?? Task.CompletedTask;

    private void OnTextReceived(object? sender, string text)
    {
        if (!_disposed)
            OutputReceived?.Invoke(this, text);
    }

    private void OnShellClosed(object? sender, EventArgs e)
    {
        OnUiThread(() =>
        {
            IsConnected = false;
            StatusText = "Shell closed.";
        });
    }

    private void ConnectionService_ConnectionStateChanged(object? sender, bool isConnected)
    {
        if (!isConnected)
        {
            OnUiThread(() =>
            {
                IsConnected = false;
                StatusText = "SSH connection lost.";
            });
        }
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
            return;

        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _connectionService.ConnectionStateChanged -= ConnectionService_ConnectionStateChanged;
        if (_shellSession is not null)
        {
            _shellSession.TextReceived -= OnTextReceived;
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
