using MibExplorer.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Globalization;
using System.IO;

namespace MibExplorer.Services;

public sealed class SshMibConnectionService : IMibConnectionService
{
    private SshClient? _sshClient;

    public bool IsConnected => _sshClient?.IsConnected == true;

    public async Task DownloadFileAsync(
    string remotePath,
    string localPath,
    IProgress<FileTransferProgressInfo>? progress = null,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (string.IsNullOrWhiteSpace(remotePath))
            throw new InvalidOperationException("Remote path is required.");

        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException("Local path is required.");

        string? directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var connectionInfo = _sshClient?.ConnectionInfo
            ?? throw new InvalidOperationException("SSH connection info is not available.");

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var scp = new ScpClient(connectionInfo);

            scp.Downloading += (_, e) =>
            {
                progress?.Report(new FileTransferProgressInfo
                {
                    Operation = "Download",
                    SourcePath = remotePath,
                    DestinationPath = localPath,
                    BytesTransferred = (ulong)e.Downloaded,
                    TotalBytes = (ulong)e.Size
                });
            };

            scp.Connect();

            try
            {
                using var localStream = System.IO.File.Create(localPath);
                scp.Download(remotePath, localStream);
                localStream.Flush();
            }
            finally
            {
                if (scp.IsConnected)
                    scp.Disconnect();
            }
        }, cancellationToken);
    }

    private async Task UploadFileCoreAsync(
    string localPath,
    string remotePath,
    IProgress<FileTransferProgressInfo>? progress = null,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException("Local path is required.");

        if (string.IsNullOrWhiteSpace(remotePath))
            throw new InvalidOperationException("Remote path is required.");

        if (!System.IO.File.Exists(localPath))
            throw new FileNotFoundException("Local file not found.", localPath);

        var connectionInfo = _sshClient?.ConnectionInfo
            ?? throw new InvalidOperationException("SSH connection info is not available.");

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong totalBytes = (ulong)new FileInfo(localPath).Length;

            using var scp = new ScpClient(connectionInfo);

            scp.Uploading += (_, e) =>
            {
                progress?.Report(new FileTransferProgressInfo
                {
                    Operation = "Upload",
                    SourcePath = localPath,
                    DestinationPath = remotePath,
                    BytesTransferred = (ulong)e.Uploaded,
                    TotalBytes = (ulong)e.Size
                });
            };

            scp.Connect();

            try
            {
                using var localStream = System.IO.File.OpenRead(localPath);
                scp.Upload(localStream, remotePath);

                progress?.Report(new FileTransferProgressInfo
                {
                    Operation = "Upload",
                    SourcePath = localPath,
                    DestinationPath = remotePath,
                    BytesTransferred = totalBytes,
                    TotalBytes = totalBytes
                });
            }
            finally
            {
                if (scp.IsConnected)
                    scp.Disconnect();
            }
        }, cancellationToken);
    }

    public async Task UploadFileAsync(
    string localPath,
    string remotePath,
    IProgress<FileTransferProgressInfo>? progress = null,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        await RunWritableOperationAsync(remotePath, async ct =>
        {
            await UploadFileCoreAsync(localPath, remotePath, progress, ct);
        }, cancellationToken);
    }

    public async Task ReplaceFileAsync(
    string localPath,
    string remotePath,
    IProgress<FileTransferProgressInfo>? progress = null,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException("Local path is required.");

        if (string.IsNullOrWhiteSpace(remotePath))
            throw new InvalidOperationException("Remote path is required.");

        if (!System.IO.File.Exists(localPath))
            throw new FileNotFoundException("Local file not found.", localPath);

        string tempRemotePath = BuildTemporaryRemotePath(remotePath);
        string backupRemotePath = BuildBackupRemotePath(remotePath);

        await RunWritableOperationAsync(remotePath, async ct =>
        {
            await ExecuteCommandAsync($"rm -f {EscapeShellArg(tempRemotePath)}", ct);
            await ExecuteCommandAsync($"rm -f {EscapeShellArg(backupRemotePath)}", ct);

            await UploadFileCoreAsync(localPath, tempRemotePath, progress, ct);

            bool originalExists = await RemotePathExistsAsync(remotePath, ct);

            if (originalExists)
            {
                await ExecuteCommandAsync(
                    $"mv -f {EscapeShellArg(remotePath)} {EscapeShellArg(backupRemotePath)}",
                    ct);
            }

            try
            {
                await ExecuteCommandAsync(
                    $"mv -f {EscapeShellArg(tempRemotePath)} {EscapeShellArg(remotePath)}",
                    ct);

                await ExecuteCommandAsync(
                    $"rm -f {EscapeShellArg(backupRemotePath)}",
                    ct);
            }
            catch
            {
                if (await RemotePathExistsAsync(backupRemotePath, ct) &&
                    !await RemotePathExistsAsync(remotePath, ct))
                {
                    try
                    {
                        await ExecuteCommandAsync(
                            $"mv -f {EscapeShellArg(backupRemotePath)} {EscapeShellArg(remotePath)}",
                            ct);
                    }
                    catch
                    {
                        // Best effort restore only
                    }
                }

                throw;
            }
            finally
            {
                try
                {
                    await ExecuteCommandAsync(
                        $"rm -f {EscapeShellArg(tempRemotePath)}",
                        ct);
                }
                catch
                {
                }
            }
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
                $"rm -f {EscapeShellArg(remotePath)}",
                ct);
        }, cancellationToken);
    }

    public Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DisconnectInternal();

        if (string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException("Host is required.");

        if (string.IsNullOrWhiteSpace(settings.Username))
            throw new InvalidOperationException("Username is required.");

        ConnectionInfo connectionInfo;

        if (settings.UsePrivateKey)
        {
            if (string.IsNullOrWhiteSpace(settings.PrivateKeyPath))
                throw new InvalidOperationException("Private key path is required.");

            if (!File.Exists(settings.PrivateKeyPath))
                throw new FileNotFoundException("Private key file not found.", settings.PrivateKeyPath);

            PrivateKeyFile privateKeyFile = string.IsNullOrWhiteSpace(settings.Passphrase)
                ? new PrivateKeyFile(settings.PrivateKeyPath)
                : new PrivateKeyFile(settings.PrivateKeyPath, settings.Passphrase);

            connectionInfo = new ConnectionInfo(
                settings.Host,
                settings.Port,
                settings.Username,
                new PrivateKeyAuthenticationMethod(settings.Username, privateKeyFile));
        }
        else
        {
            connectionInfo = new ConnectionInfo(
                settings.Host,
                settings.Port,
                settings.Username,
                new PasswordAuthenticationMethod(settings.Username, settings.Password ?? string.Empty));
        }

        connectionInfo.Timeout = TimeSpan.FromSeconds(10);

        _sshClient = new SshClient(connectionInfo);

        try
        {
            _sshClient.Connect();
        }
        catch
        {
            try
            {
                if (_sshClient is not null)
                {
                    if (_sshClient.IsConnected)
                        _sshClient.Disconnect();

                    _sshClient.Dispose();
                    _sshClient = null;
                }
            }
            catch
            {
            }

            throw;
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        DisconnectInternal();
        return Task.CompletedTask;
    }

    public Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        using var cmd = _sshClient!.CreateCommand(command);
        string result = cmd.Execute();

        if (cmd.ExitStatus != 0)
        {
            string message = string.IsNullOrWhiteSpace(cmd.Error)
                ? "Remote command failed."
                : cmd.Error.Trim();

            throw new SshException(message);
        }

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<RemoteExplorerItem>> GetRootEntriesAsync(CancellationToken cancellationToken = default)
    {
        return GetChildrenAsync("/", cancellationToken);
    }

    private static IReadOnlyList<RemoteExplorerItem> ParseLsLa(string parentPath, string raw)
    {
        var items = new List<RemoteExplorerItem>();

        foreach (string line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("total "))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 9)
                continue;

            string permissions = parts[0];
            string name = string.Join(' ', parts.Skip(8));

            if (name == "." || name == "..")
                continue;

            // Handle symlink (name -> target)
            if (name.Contains(" -> "))
                name = name.Split(" -> ")[0];

            RemoteEntryType type = permissions[0] switch
            {
                'd' => RemoteEntryType.Directory,
                'l' => RemoteEntryType.Symlink,
                '-' => RemoteEntryType.File,
                _ => RemoteEntryType.Unknown
            };

            long size = 0;
            long.TryParse(parts[4], out size);

            string fullPath = parentPath == "/"
                ? "/" + name
                : parentPath.TrimEnd('/') + "/" + name;

            items.Add(new RemoteExplorerItem
            {
                Name = name,
                FullPath = fullPath,
                EntryType = type,
                Size = type == RemoteEntryType.File ? size : 0,
                ModifiedAt = null
            });
        }

        return items;
    }

    public Task<IReadOnlyList<RemoteExplorerItem>> GetChildrenAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        string normalizedPath = NormalizeRemotePath(remotePath);
        string escapedPath = EscapeShellArg(normalizedPath);

        string commandText =
            "sh -c " +
            EscapeShellArg(
                $"cd {escapedPath} 2>/dev/null || exit 2; " +
                "ls -la");

        using var cmd = _sshClient!.CreateCommand(commandText);
        string result = cmd.Execute();

        if (cmd.ExitStatus != 0)
        {
            string message = string.IsNullOrWhiteSpace(cmd.Error)
                ? $"Failed to list remote path: {normalizedPath}"
                : cmd.Error.Trim();

            throw new SshException(message);
        }

        IReadOnlyList<RemoteExplorerItem> items = ParseLsLa(normalizedPath, result);
        return Task.FromResult(items);
    }

    public void Dispose()
    {
        DisconnectInternal();
    }

    private void EnsureConnected()
    {
        if (_sshClient?.IsConnected != true)
            throw new InvalidOperationException("SSH client is not connected.");
    }

    private void DisconnectInternal()
    {
        if (_sshClient is null)
            return;

        try
        {
            if (_sshClient.IsConnected)
                _sshClient.Disconnect();
        }
        finally
        {
            _sshClient.Dispose();
            _sshClient = null;
        }
    }

    private static string NormalizeRemotePath(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
            return "/";

        return remotePath.Trim();
    }

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

    private async Task RunWritableOperationAsync(
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

    private static string BuildTemporaryRemotePath(string remotePath)
    {
        return remotePath + ".__mibexplorer_tmp__";
    }

    private static string BuildBackupRemotePath(string remotePath)
    {
        return remotePath + ".__mibexplorer_bak__";
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

    private static string EscapeShellArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "''";

        return "'" + arg.Replace("'", "'\"'\"'") + "'";
    }
}
