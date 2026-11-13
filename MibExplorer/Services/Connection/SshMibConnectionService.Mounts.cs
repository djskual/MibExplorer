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
        // Synchronous preflight only validates the path. Do not restrict edits to a
        // fixed list of mount points: QNX enforces the actual filesystem permissions.
        string normalized = NormalizeRemotePath(remotePath).Replace('\\', '/').TrimEnd('/');
        int separator = normalized.LastIndexOf('/');
        string entryName = normalized[(separator + 1)..];
        return normalized.StartsWith("/", StringComparison.Ordinal) &&
               normalized.Length > 1 &&
               !normalized.Contains('\r') && !normalized.Contains('\n') && !normalized.Contains('\0') &&
               entryName is not ("" or "." or "..") &&
               IsRccWriteTargetAllowed(normalized);
    }

    public async Task<bool> CanWriteToPathAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        try
        {
            // Only verify that the parent exists and its symlinks resolve.
            // The real write attempt (or existing mount policy) decides writability.
            string parent = await ResolveWriteParentDirectoryAsync(remotePath, cancellationToken);
            return IsRccWriteTargetAllowed(remotePath) && IsRccWriteDirectoryAllowed(parent);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SshException)
        {
            return false; // e.g. broken parent link, inaccessible directory
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // Resolve all symbolic links in the PARENT directory, but never resolve the final
    // entry. A delete/rename of a symlink must affect the link, not its target.
    // Authorize writes based on the parent filesystem, not the final name:
    // deleting /tmp or /fs/sda0 themselves must never be treated as a normal write.
    // Also works for a new file/directory that does not yet exist.
    private async Task<string> ResolveWriteParentDirectoryAsync(
        string remotePath,
        CancellationToken cancellationToken)
    {
        string path = NormalizeRemotePath(remotePath).Replace('\\', '/').TrimEnd('/');
        if (!path.StartsWith("/", StringComparison.Ordinal) || path.Length <= 1 ||
            path.Contains('\r') || path.Contains('\n') || path.Contains('\0'))
            throw new ArgumentException("A valid absolute remote file path is required.", nameof(remotePath));

        int separator = path.LastIndexOf('/');
        string entryName = path[(separator + 1)..];
        if (entryName is "" or "." or "..")
            throw new ArgumentException("Invalid remote entry name.", nameof(remotePath));

        string parentPath = separator == 0 ? "/" : path[..separator];
        string physicalParent = (await ExecuteCommandAsync(
            $"cd {EscapeShellArg(parentPath)} && pwd -P", cancellationToken)).Trim();

        if (!physicalParent.StartsWith("/", StringComparison.Ordinal) ||
            physicalParent.Contains('\r') || physicalParent.Contains('\n'))
            throw new InvalidOperationException($"Unable to resolve remote parent directory: {parentPath}");

        return physicalParent;
    }

    public async Task<bool> IsRemoteSymbolicLinkAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        string escapedPath = EscapeShellArg(NormalizeRemotePath(remotePath));
        try
        {
            // The QNX ls format already used by the explorer starts symlinks with 'l'.
            string details = (await ExecuteCommandAsync($"ls -ld {escapedPath}", cancellationToken)).TrimStart();
            return details.Length > 0 && details[0] == 'l';
        }
        catch (SshException ex) when (
            ex.Message.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase))
        {
            return false; // A new destination does not have a final symlink yet.
        }
    }

    // The Explorer policy protects RCC while keeping MMX writes governed by QNX.
    // Match both /net/rcc and fully-qualified Qnet node names (/net/rcc.*).
    private static string? GetRccRelativePath(string remotePath)
    {
        string path = NormalizeRemotePath(remotePath).Replace('\\', '/').TrimEnd('/');
        if (path.Equals("/net/rcc", StringComparison.Ordinal))
            return "";
        if (path.StartsWith("/net/rcc/", StringComparison.Ordinal))
            return path["/net/rcc".Length..];
        if (path.StartsWith("/net/rcc.", StringComparison.Ordinal))
        {
            int separator = path.IndexOf('/', "/net/rcc.".Length);
            return separator < 0 ? "" : path[separator..];
        }
        return null; // Not an RCC path: leave QNX permissions in control.
    }

    private static bool IsRccWriteDirectoryAllowed(string directoryPath)
    {
        string? rccRelative = GetRccRelativePath(directoryPath);
        return rccRelative is null ||
               IsPathAtOrBelow(rccRelative, "/mnt/efs-persist") ||
               IsPathAtOrBelow(rccRelative, "/mnt/efs-system");
    }

    // Check the parent, never the target entry. The mounted efs-* roots themselves
    // cannot be deleted/renamed, but their children can be modified.
    private static bool IsRccWriteTargetAllowed(string remotePath)
    {
        string path = NormalizeRemotePath(remotePath).Replace('\\', '/').TrimEnd('/');
        int separator = path.LastIndexOf('/');
        string parent = separator <= 0 ? "/" : path[..separator];
        return IsRccWriteDirectoryAllowed(parent);
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

        await _writableOperationSemaphore.WaitAsync(cancellationToken);
        var remountedByUs = new List<string>();
        try
        {
            // Resolve under the same lock as the mount operation. This works for
            // nested directory symlinks and avoids hard-coded /root-style aliases.
            string parent = await ResolveWriteParentDirectoryAsync(remotePath, cancellationToken);
            if (!IsRccWriteTargetAllowed(remotePath) || !IsRccWriteDirectoryAllowed(parent))
                throw new InvalidOperationException(
                    "RCC is read-only in MibExplorer outside /net/rcc/mnt/efs-persist and /net/rcc/mnt/efs-system.");

            if (IsDirectlyWritablePath(parent))
            {
                await operation(cancellationToken);
                return;
            }

            var mountPoints = ResolveWritableMounts(parent);
            if (mountPoints.Count == 0)
            {
                // An unmapped directory may already be writable (for example,
                // /net/mmx/var/app). Let QNX authorize or reject the operation.
                // Never attempt a guessed mount -uw on an unknown filesystem.
                await operation(cancellationToken);
                return;
            }

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
