namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
    public bool CanWriteToPath(string remotePath)
    {
        var mounts = ResolveWritableMounts(remotePath);
        return mounts.Count > 0;
    }

    private static IReadOnlyList<string> ResolveWritableMounts(string remotePath)
    {
        string normalized = NormalizeRemotePath(remotePath).Replace('\\', '/');

        var mounts = new List<string>();

        if (normalized.StartsWith("/eso/", StringComparison.Ordinal) ||
            normalized.Equals("/eso", StringComparison.Ordinal) ||
            normalized.StartsWith("/mnt/app/", StringComparison.Ordinal) ||
            normalized.Equals("/mnt/app", StringComparison.Ordinal))
        {
            mounts.Add("/net/mmx/mnt/app");
        }

        if (normalized.StartsWith("/mnt/system/", StringComparison.Ordinal) ||
            normalized.Equals("/mnt/system", StringComparison.Ordinal))
        {
            mounts.Add("/net/mmx/mnt/system");
        }

        if (normalized.StartsWith("/net/rcc/mnt/efs-persist/", StringComparison.Ordinal) ||
            normalized.Equals("/net/rcc/mnt/efs-persist", StringComparison.Ordinal))
        {
            mounts.Add("/net/rcc/mnt/efs-persist");
        }

        return mounts;
    }

    private async Task MountWritableAsync(IEnumerable<string> mountPoints, CancellationToken cancellationToken = default)
    {
        foreach (string mountPoint in mountPoints.Distinct(StringComparer.Ordinal))
        {
            await ExecuteCommandAsync($"mount -uw {EscapeShellArg(mountPoint)}", cancellationToken);
        }
    }

    private async Task MountReadOnlyAsync(IEnumerable<string> mountPoints, CancellationToken cancellationToken = default)
    {
        foreach (string mountPoint in mountPoints.Distinct(StringComparer.Ordinal))
        {
            try
            {
                await ExecuteCommandAsync($"mount -ur {EscapeShellArg(mountPoint)}", cancellationToken);
            }
            catch
            {
                // Cleanup should not hide the original failure.
            }
        }
    }

    public async Task RunWritableOperationAsync(
        string remotePath,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        var mountPoints = ResolveWritableMounts(remotePath);

        if (mountPoints.Count == 0)
            throw new InvalidOperationException($"No writable mount mapping is defined for path: {remotePath}");

        await MountWritableAsync(mountPoints, cancellationToken);

        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            await MountReadOnlyAsync(mountPoints, cancellationToken);
        }
    }
}
