using MibExplorer.Models;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace MibExplorer.Services;

public sealed partial class SshMibConnectionService
{
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
}
