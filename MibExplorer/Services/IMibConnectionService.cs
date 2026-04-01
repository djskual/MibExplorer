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
}
