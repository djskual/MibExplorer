using MibExplorer.Models;
using Renci.SshNet;
using System.IO;

namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
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
}
