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
            char entryKind = permissions[0];
            string? target = null;

            // Preserve the actual link target. In Qnet an absolute target must
            // be rebased onto its owning node before it is traversed.
            if (entryKind == 'l')
            {
                int arrow = name.LastIndexOf(" -> ", StringComparison.Ordinal);
                if (arrow >= 0)
                {
                    target = name[(arrow + 4)..];
                    name = name[..arrow];
                }
            }

            if (name == "." || name == "..")
                continue;

            RemoteEntryType type = entryKind switch
            {
                'd' => RemoteEntryType.Directory,
                'l' => RemoteEntryType.Symlink,
                '-' => RemoteEntryType.File,

                // QNX / Unix special entries we want to treat as downloadable files
                'c' => RemoteEntryType.File,
                'b' => RemoteEntryType.File,
                'p' => RemoteEntryType.File,
                's' => RemoteEntryType.File,

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
                SymlinkTarget = target,
                Size = type == RemoteEntryType.Directory ? 0 : size,
                ModifiedAt = null
            });
        }

        return items;
    }

    public Task<IReadOnlyList<RemoteExplorerItem>> GetChildrenAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedPath = NormalizeRemotePath(remotePath);
        string escapedPath = EscapeShellArg(normalizedPath);

        string commandText =
            "sh -c " +
            EscapeShellArg(
                $"cd {escapedPath} 2>/dev/null || exit 2; " +
                "ls -la");

        // SSH.NET Execute() is synchronous. Running it on the caller thread
        // freezes WPF during slow Qnet listings. Do not change path semantics.
        return Task.Run<IReadOnlyList<RemoteExplorerItem>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureConnected();
            using var cmd = _sshClient!.CreateCommand(commandText);
            string result = cmd.Execute();

            if (cmd.ExitStatus != 0)
            {
                string message = string.IsNullOrWhiteSpace(cmd.Error)
                    ? $"Failed to list remote path: {normalizedPath}"
                    : cmd.Error.Trim();

                throw new SshException(message);
            }

            return ParseLsLa(normalizedPath, result);
        }, cancellationToken);
    }
}
