namespace MibExplorer.Services;

public interface IRemoteShellSession : IDisposable
{
    bool IsOpen { get; }

    event EventHandler<string>? TextReceived;
    event EventHandler? Closed;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);
}