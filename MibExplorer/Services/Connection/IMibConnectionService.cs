using MibExplorer.Models;

namespace MibExplorer.Services;

public interface IMibConnectionService : IDisposable
{
    bool IsConnected { get; }

    event EventHandler<bool>? ConnectionStateChanged;

    Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
    Task<bool> ProbeConnectionAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    Task<IRemoteShellSession> CreateShellSessionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RemoteExplorerItem>> GetRootEntriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemoteExplorerItem>> GetChildrenAsync(string remotePath, CancellationToken cancellationToken = default);

    Task<string> ReadRemoteTextFileAsync(string remotePath, CancellationToken cancellationToken = default);

    Task DownloadFileAsync(
        string remotePath,
        string localPath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(
        string localPath,
        string remotePath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> RemotePathExistsAsync(
        string remotePath,
        CancellationToken cancellationToken = default);

    Task ReplaceFileAsync(
        string localPath,
        string remotePath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    bool CanWriteToPath(string remotePath);

    Task RenamePathAsync(
        string remotePath,
        string newName,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(
        string remotePath,
        CancellationToken cancellationToken = default);

    Task RunWritableOperationAsync(
        string remotePath,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task CreateDirectoryWithoutMountAsync(string remotePath, CancellationToken cancellationToken = default);

    Task UploadFileWithoutMountAsync(
        string localPath,
        string remotePath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task DeletePathWithoutMountAsync(string remotePath, CancellationToken cancellationToken = default);

    Task MovePathWithoutMountAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    Task DownloadFilesBatchAsync(
        IReadOnlyList<(string RemotePath, string LocalPath, IProgress<FileTransferProgressInfo>? Progress)> files,
        CancellationToken cancellationToken = default);
}
