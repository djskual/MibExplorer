using MibExplorer.Models;

namespace MibExplorer.Services;

public interface IMibConnectionService
{
    Task<IReadOnlyList<RemoteExplorerItem>> GetRootEntriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemoteExplorerItem>> GetChildrenAsync(string remotePath, CancellationToken cancellationToken = default);
}
