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
        _sshClient.Connect();

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

    public Task<string> DebugListCommandAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnected();

        string normalizedPath = NormalizeRemotePath(remotePath);
        string escapedPath = EscapeShellArg(normalizedPath);

        string commandText =
            "sh -c " +
            EscapeShellArg(
                $"cd {escapedPath} 2>/dev/null || exit 2; " +
                "pwd; " +
                "echo '---'; " +
                "ls -la 2>/dev/null; " +
                "echo '---'; " +
                "ls -1A 2>/dev/null");

        using var cmd = _sshClient!.CreateCommand(commandText);
        string result = cmd.Execute();

        if (cmd.ExitStatus != 0)
        {
            string message = string.IsNullOrWhiteSpace(cmd.Error)
                ? $"Debug list command failed for: {normalizedPath}"
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

    private static string EscapeShellArg(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private static IReadOnlyList<RemoteExplorerItem> ParseListing(string parentPath, string raw)
    {
        var items = new List<RemoteExplorerItem>();

        foreach (string line in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('|');
            if (parts.Length < 3)
                continue;

            string type = parts[0].Trim();
            string name = parts[1].Trim();
            string sizeRaw = parts[2].Trim();

            if (string.IsNullOrWhiteSpace(name))
                continue;

            long size = 0;
            long.TryParse(sizeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);

            string fullPath = parentPath == "/"
                ? "/" + name
                : parentPath.TrimEnd('/') + "/" + name;

            items.Add(new RemoteExplorerItem
            {
                Name = name,
                FullPath = fullPath,
                EntryType = type switch
                {
                    "D" => RemoteEntryType.Directory,
                    "F" => RemoteEntryType.File,
                    "L" => RemoteEntryType.Symlink,
                    _ => RemoteEntryType.Unknown
                },
                Size = size,
                ModifiedAt = null
            });
        }

        return items;
    }
}
