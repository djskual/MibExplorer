using Renci.SshNet.Common;
using System.IO;
using System.Threading;

namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
    // Prevent two file operations from changing the same mount's state concurrently.
    private readonly SemaphoreSlim _writableOperationSemaphore = new(1, 1);

    public bool CanWriteToPath(string remotePath)
    {
        string normalized = NormalizeRemotePath(remotePath).Replace('\\', '/');
        return IsDirectlyWritablePath(normalized) || ResolveWritableMounts(normalized).Count > 0;
    }

    private static bool IsDirectlyWritablePath(string normalized)
    {
        // QNX /tmp does not need a mount -uw / mount -ur cycle.
        return IsPathAtOrBelow(normalized, "/tmp") ||
               IsPathAtOrBelow(normalized, "/dev/shmem");
    }

    private static bool IsPathAtOrBelow(string path, string root)
    {
        return path.Equals(root, StringComparison.Ordinal) ||
               path.StartsWith(root + "/", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ResolveWritableMounts(string remotePath)
    {
        string normalized = NormalizeRemotePath(remotePath).Replace('\\', '/');

        var mounts = new List<string>();

        if (IsPathAtOrBelow(normalized, "/eso") ||
            IsPathAtOrBelow(normalized, "/mnt/app") ||
            IsPathAtOrBelow(normalized, "/net/mmx/mnt/app"))
        {
            mounts.Add("/net/mmx/mnt/app");
        }

        if (IsPathAtOrBelow(normalized, "/mnt/system") ||
            IsPathAtOrBelow(normalized, "/net/mmx/mnt/system"))
        {
            mounts.Add("/net/mmx/mnt/system");
        }

        if (IsPathAtOrBelow(normalized, "/net/rcc/mnt/efs-persist"))
        {
            mounts.Add("/net/rcc/mnt/efs-persist");
        }

        // QNX SD cards: map both the local and MMX-qualified paths to the
        // same mount points already used by the SD installer scripts.
        if (IsPathAtOrBelow(normalized, "/fs/sda0") ||
            IsPathAtOrBelow(normalized, "/net/mmx/fs/sda0"))
        {
            mounts.Add("/net/mmx/fs/sda0");
        }

        if (IsPathAtOrBelow(normalized, "/fs/sdb0") ||
            IsPathAtOrBelow(normalized, "/net/mmx/fs/sdb0"))
        {
            mounts.Add("/net/mmx/fs/sdb0");
        }

        return mounts;
    }

    private async Task<bool> IsMountWritableAsync(string mountPoint, CancellationToken cancellationToken)
    {
        // On this QNX version, `mount` does not print RO/RW flags. Check by creating
        // and immediately deleting a unique empty file on the target filesystem.
        string probePath = mountPoint.TrimEnd('/') + "/.mibexplorer_rw_probe_" + Guid.NewGuid().ToString("N");
        string escapedProbe = EscapeShellArg(probePath);
        string script =
            $"if [ -e {escapedProbe} ]; then echo 'RW probe collision' >&2; exit 1; fi; " +
            $": > {escapedProbe} && rm -f {escapedProbe}";

        try
        {
            await ExecuteCommandAsync($"sh -c {EscapeShellArg(script)}", cancellationToken);
            return true;
        }
        catch (SshException ex) when (
            ex.Message.Contains("Read-only file system", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("EROFS", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    public async Task RunWritableOperationAsync(
        string remotePath,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        string normalized = NormalizeRemotePath(remotePath).Replace('\\', '/');
        if (IsDirectlyWritablePath(normalized))
        {
            await operation(cancellationToken);
            return;
        }

        var mountPoints = ResolveWritableMounts(normalized);
        if (mountPoints.Count == 0)
            throw new InvalidOperationException($"No writable mount mapping is defined for path: {remotePath}");

        await _writableOperationSemaphore.WaitAsync(cancellationToken);
        var remountedByUs = new List<string>();
        try
        {
            foreach (string mountPoint in mountPoints.Distinct(StringComparer.Ordinal))
            {
                if (await IsMountWritableAsync(mountPoint, cancellationToken))
                    continue; // Initially RW: leave it RW, both now and after the operation.

                await ExecuteCommandAsync($"mount -uw {EscapeShellArg(mountPoint)}", cancellationToken);
                remountedByUs.Add(mountPoint);

                if (!await IsMountWritableAsync(mountPoint, cancellationToken))
                    throw new IOException($"Mount is still read-only after mount -uw: {mountPoint}");
            }

            await operation(cancellationToken);
        }
        finally
        {
            try
            {
                if (remountedByUs.Count > 0)
                {
                    try
                    {
                        await ExecuteCommandAsync("sync", CancellationToken.None);
                    }
                    finally
                    {
                        // Cleanup must run even when the operation was cancelled.
                        for (int i = remountedByUs.Count - 1; i >= 0; i--)
                        {
                            await ExecuteCommandAsync(
                                $"mount -ur {EscapeShellArg(remountedByUs[i])}",
                                CancellationToken.None);
                        }
                    }
                }
            }
            finally
            {
                _writableOperationSemaphore.Release();
            }
        }
    }
}
