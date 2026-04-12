namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
    public async Task<string> ReadRemoteTextFileAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (string.IsNullOrWhiteSpace(remotePath))
            throw new InvalidOperationException("Remote path is required.");

        string normalizedPath = NormalizeRemotePath(remotePath);
        string escapedPath = EscapeShellArg(normalizedPath);

        string command = $"sh -c {EscapeShellArg($"if [ ! -f {escapedPath} ]; then echo File not found. >&2; exit 1; fi; cat {escapedPath}")}";

        return await Task.Run(
            () => ExecuteCommandAsync(command, cancellationToken),
            cancellationToken);
    }
}