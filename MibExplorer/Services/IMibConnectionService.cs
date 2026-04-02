using MibExplorer.Models;

namespace MibExplorer.Services;

public interface IMibConnectionService : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RemoteExplorerItem>> GetRootEntriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemoteExplorerItem>> GetChildrenAsync(string remotePath, CancellationToken cancellationToken = default);

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
}
