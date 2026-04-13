namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
    public async Task RenamePathAsync(
    string remotePath,
    string newName,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (string.IsNullOrWhiteSpace(remotePath))
            throw new InvalidOperationException("Remote path is required.");

        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("New name is required.");

        if (newName.Contains('/') || newName.Contains('\\'))
            throw new InvalidOperationException("New name must not contain path separators.");

        if (newName == "." || newName == "..")
            throw new InvalidOperationException("Invalid target name.");

        string normalizedPath = remotePath.Replace('\\', '/');
        int lastSlashIndex = normalizedPath.LastIndexOf('/');
        string parentPath = lastSlashIndex <= 0 ? "/" : normalizedPath[..lastSlashIndex];
        string targetPath = parentPath == "/"
            ? "/" + newName
            : parentPath + "/" + newName;

        if (string.Equals(normalizedPath, targetPath, StringComparison.Ordinal))
            return;

        await RunWritableOperationAsync(remotePath, async ct =>
        {
            bool targetExists = await RemotePathExistsAsync(targetPath, ct);
            if (targetExists)
                throw new InvalidOperationException($"A file or folder named '{newName}' already exists in the target directory.");

            await ExecuteCommandAsync(
                $"mv {EscapeShellArg(normalizedPath)} {EscapeShellArg(targetPath)}",
                ct);
        }, cancellationToken);
    }

    public async Task DeleteFileAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (string.IsNullOrWhiteSpace(remotePath))
            throw new InvalidOperationException("Remote path is required.");

        await RunWritableOperationAsync(remotePath, async ct =>
        {
            await ExecuteCommandAsync(
                $"rm -rf {EscapeShellArg(remotePath)}",
                ct);
        }, cancellationToken);
    }

    public async Task<bool> RemotePathExistsAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteCommandAsync(
                $"sh -c {EscapeShellArg($"test -e {EscapeShellArg(remotePath)} && echo exists")}",
                cancellationToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateDirectoryWithoutMountAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        string escapedPath = EscapeShellArg(NormalizeRemotePath(remotePath));
        await ExecuteCommandAsync($"mkdir -p {escapedPath}", cancellationToken);
    }

    public async Task DeletePathWithoutMountAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        string escapedPath = EscapeShellArg(NormalizeRemotePath(remotePath));
        await ExecuteCommandAsync($"rm -rf {escapedPath}", cancellationToken);
    }

    public async Task MovePathWithoutMountAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        string escapedSource = EscapeShellArg(NormalizeRemotePath(sourcePath));
        string escapedDestination = EscapeShellArg(NormalizeRemotePath(destinationPath));

        await ExecuteCommandAsync($"mv {escapedSource} {escapedDestination}", cancellationToken);
    }
}
