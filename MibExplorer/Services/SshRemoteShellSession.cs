using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;

namespace MibExplorer.Services;

public sealed class SshRemoteShellSession : IRemoteShellSession
{
    private readonly SshClient _sshClient;
    private readonly object _syncRoot = new();
    private ShellStream? _shellStream;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private bool _disposed;

    public SshRemoteShellSession(SshClient sshClient)
    {
        _sshClient = sshClient ?? throw new ArgumentNullException(nameof(sshClient));
    }

    public bool IsOpen =>
        !_disposed &&
        _sshClient.IsConnected &&
        _shellStream is not null &&
        _shellStream.CanRead &&
        _shellStream.CanWrite;

    public event EventHandler<string>? TextReceived;
    public event EventHandler? Closed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (!_sshClient.IsConnected)
            throw new InvalidOperationException("SSH client is not connected.");

        lock (_syncRoot)
        {
            if (_shellStream is not null)
                return Task.CompletedTask;

            _shellStream = _sshClient.CreateShellStream(
                terminalName: "xterm",
                columns: 120,
                rows: 40,
                width: 800,
                height: 600,
                bufferSize: 4096);

            _readerCts = new CancellationTokenSource();
            _readerTask = Task.Run(() => ReadLoopAsync(_readerCts.Token), _readerCts.Token);
        }

        return Task.CompletedTask;
    }

    public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (command is null)
            throw new ArgumentNullException(nameof(command));

        lock (_syncRoot)
        {
            if (_shellStream is null)
                throw new InvalidOperationException("Shell session has not been started.");

            if (!_sshClient.IsConnected || !_shellStream.CanWrite)
                throw new InvalidOperationException("Shell session is not available.");

            _shellStream.Write(command);
            _shellStream.Write("\n");
            _shellStream.Flush();
        }

        return Task.CompletedTask;
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ShellStream? stream;

                lock (_syncRoot)
                    stream = _shellStream;

                if (stream is null || !_sshClient.IsConnected || !stream.CanRead)
                    break;

                if (!stream.DataAvailable)
                {
                    await Task.Delay(40, cancellationToken);
                    continue;
                }

                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead <= 0)
                    break;

                int charCount = decoder.GetCharCount(buffer, 0, bytesRead);
                var chars = new char[charCount];
                decoder.GetChars(buffer, 0, bytesRead, chars, 0);

                string text = new string(chars);
                if (!string.IsNullOrEmpty(text))
                    TextReceived?.Invoke(this, text);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SshException ex)
        {
            TextReceived?.Invoke(this, $"{Environment.NewLine}[shell error] {ex.Message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            TextReceived?.Invoke(this, $"{Environment.NewLine}[shell error] {ex.Message}{Environment.NewLine}");
        }
        finally
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _readerCts?.Cancel();
        }
        catch
        {
        }

        try
        {
            _shellStream?.Dispose();
        }
        catch
        {
        }

        try
        {
            _readerCts?.Dispose();
        }
        catch
        {
        }

        _shellStream = null;
        _readerCts = null;
        _readerTask = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SshRemoteShellSession));
    }
}