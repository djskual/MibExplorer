namespace MibExplorer.Services.Design;

public sealed class DesignRemoteShellSession : IRemoteShellSession
{
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    public event EventHandler<string>? TextReceived;
    public event EventHandler? Closed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isOpen = true;

        TextReceived?.Invoke(this,
            "MibExplorer design shell\r\n" +
            "Connected to mock target\r\n" +
            "# ");

        return Task.CompletedTask;
    }

    public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_isOpen)
            throw new InvalidOperationException("Shell session is closed.");

        command ??= string.Empty;

        TextReceived?.Invoke(this,
            command + "\r\n" +
            "[design output] Command executed in mock shell.\r\n" +
            "# ");

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_isOpen)
            return;

        _isOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}