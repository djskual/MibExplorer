namespace MibExplorer.Services;

public interface IRemoteShellSession : IDisposable
{
    bool IsOpen { get; }

    event EventHandler<string>? TextReceived;
    event EventHandler? Closed;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);

    // Raw PTY input: the terminal emulator supplies Enter/Tab/Ctrl+C/arrow sequences.
    Task WriteInputAsync(string input, CancellationToken cancellationToken = default);
    Task WriteBytesAsync(byte[] data, CancellationToken cancellationToken = default);
    Task ResizeAsync(uint columns, uint rows, CancellationToken cancellationToken = default);
}