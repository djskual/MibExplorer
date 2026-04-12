using MibExplorer.Models;
using System.IO;

namespace MibExplorer.Services.Design;

public sealed class DesignMibConnectionService : IMibConnectionService
{
    private readonly Dictionary<string, IReadOnlyList<RemoteExplorerItem>> _map;
    public event EventHandler<bool>? ConnectionStateChanged;

    public DesignMibConnectionService()
    {
        _map = BuildMap();
    }

    public bool IsConnected => true;

    public Task<string> ReadRemoteTextFileAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        string fakeContent =
            $"[Design Mode]\n\n" +
            $"File: {remotePath}\n\n" +
            $"This is a mock content used at design-time.\n" +
            $"No real SSH connection is performed.";

        return Task.FromResult(fakeContent);
    }

    public Task DownloadFileAsync(
        string remotePath,
        string localPath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        const string contentPrefix = "Design-mode download placeholder for: ";
        string content = contentPrefix + remotePath;

        progress?.Report(new FileTransferProgressInfo
        {
            Operation = "Download",
            SourcePath = remotePath,
            DestinationPath = localPath,
            BytesTransferred = 0,
            TotalBytes = (ulong)content.Length
        });

        System.IO.File.WriteAllText(localPath, content);

        progress?.Report(new FileTransferProgressInfo
        {
            Operation = "Download",
            SourcePath = remotePath,
            DestinationPath = localPath,
            BytesTransferred = (ulong)content.Length,
            TotalBytes = (ulong)content.Length
        });

        return Task.CompletedTask;
    }

    public Task UploadFileAsync(
        string localPath,
        string remotePath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ulong totalBytes = 0;
        if (System.IO.File.Exists(localPath))
            totalBytes = (ulong)new FileInfo(localPath).Length;

        progress?.Report(new FileTransferProgressInfo
        {
            Operation = "Upload",
            SourcePath = localPath,
            DestinationPath = remotePath,
            BytesTransferred = 0,
            TotalBytes = totalBytes
        });

        progress?.Report(new FileTransferProgressInfo
        {
            Operation = "Upload",
            SourcePath = localPath,
            DestinationPath = remotePath,
            BytesTransferred = totalBytes,
            TotalBytes = totalBytes
        });

        return Task.CompletedTask;
    }

    public Task<bool> RemotePathExistsAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task ReplaceFileAsync(
        string localPath,
        string remotePath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ulong totalBytes = 0;
        if (System.IO.File.Exists(localPath))
            totalBytes = (ulong)new FileInfo(localPath).Length;

        progress?.Report(new FileTransferProgressInfo
        {
            Operation = "Replace",
            SourcePath = localPath,
            DestinationPath = remotePath,
            BytesTransferred = 0,
            TotalBytes = totalBytes
        });

        progress?.Report(new FileTransferProgressInfo
        {
            Operation = "Replace",
            SourcePath = localPath,
            DestinationPath = remotePath,
            BytesTransferred = totalBytes,
            TotalBytes = totalBytes
        });

        return Task.CompletedTask;
    }

    public Task RenamePathAsync(
        string remotePath,
        string newName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public bool CanWriteToPath(string remotePath)
    {
        return true;
    }

    public Task ConnectAsync(ConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectionStateChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        ConnectionStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("design-mode");
    }

    public Task<bool> ProbeConnectionAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public Task<IRemoteShellSession> CreateShellSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IRemoteShellSession>(new DesignRemoteShellSession());
    }

    public Task<IReadOnlyList<RemoteExplorerItem>> GetRootEntriesAsync(CancellationToken cancellationToken = default)
    {
        return GetChildrenAsync("/", cancellationToken);
    }

    public Task<IReadOnlyList<RemoteExplorerItem>> GetChildrenAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _map.TryGetValue(remotePath, out var entries);
        return Task.FromResult(entries ?? (IReadOnlyList<RemoteExplorerItem>)Array.Empty<RemoteExplorerItem>());
    }

    public void Dispose()
    {
    }

    public Task RunWritableOperationAsync(
        string remotePath,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        return operation(cancellationToken);
    }

    public Task CreateDirectoryWithoutMountAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UploadFileWithoutMountAsync(
        string localPath,
        string remotePath,
        IProgress<FileTransferProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ulong totalBytes = 0;
        if (System.IO.File.Exists(localPath))
            totalBytes = (ulong)new FileInfo(localPath).Length;

        progress?.Report(new FileTransferProgressInfo
        {
            Operation = "Upload",
            SourcePath = localPath,
            DestinationPath = remotePath,
            BytesTransferred = totalBytes,
            TotalBytes = totalBytes
        });

        return Task.CompletedTask;
    }

    public Task DeletePathWithoutMountAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task MovePathWithoutMountAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static Dictionary<string, IReadOnlyList<RemoteExplorerItem>> BuildMap()
    {
        var map = new Dictionary<string, IReadOnlyList<RemoteExplorerItem>>(StringComparer.Ordinal);

        map["/"] =
        [
            Dir("eso", "/eso"),
            Dir("mnt", "/mnt"),
            Dir("net", "/net"),
            Dir("tsd", "/tsd"),
            File("version.txt", "/version.txt", 1842),
        ];

        map["/eso"] =
        [
            Dir("hmi", "/eso/hmi"),
            Dir("system", "/eso/system"),
            File("manifest.xml", "/eso/manifest.xml", 45219),
        ];

        map["/eso/hmi"] =
        [
            Dir("lsd", "/eso/hmi/lsd"),
            Dir("Data", "/eso/hmi/Data"),
            File("boot.skin", "/eso/hmi/boot.skin", 9216),
        ];

        map["/eso/hmi/lsd"] =
        [
            Dir("Resources", "/eso/hmi/lsd/Resources"),
            File("imageIdMap.res", "/eso/hmi/lsd/imageIdMap.res", 287341),
        ];

        map["/eso/hmi/lsd/Resources"] =
        [
            Dir("skin0", "/eso/hmi/lsd/Resources/skin0"),
            Dir("skin1", "/eso/hmi/lsd/Resources/skin1"),
        ];

        map["/eso/hmi/lsd/Resources/skin0"] =
        [
            File("images.mcf", "/eso/hmi/lsd/Resources/skin0/images.mcf", 64223872),
            File("theme.ini", "/eso/hmi/lsd/Resources/skin0/theme.ini", 1024),
        ];

        map["/eso/hmi/Data"] =
        [
            Dir("VWMQB", "/eso/hmi/Data/VWMQB"),
        ];

        map["/eso/hmi/Data/VWMQB"] =
        [
            Dir("AmbianceLight", "/eso/hmi/Data/VWMQB/AmbianceLight"),
            File("vehicle.xml", "/eso/hmi/Data/VWMQB/vehicle.xml", 24333),
        ];

        map["/eso/hmi/Data/VWMQB/AmbianceLight"] =
        [
            Dir("AmbianceLighttest", "/eso/hmi/Data/VWMQB/AmbianceLight/AmbianceLighttest"),
            File("0.gca", "/eso/hmi/Data/VWMQB/AmbianceLight/0.gca", 8192),
            File("1.gca", "/eso/hmi/Data/VWMQB/AmbianceLight/1.gca", 8192),
        ];

        map["/eso/system"] =
        [
            File("startup.conf", "/eso/system/startup.conf", 617),
            File("services.ini", "/eso/system/services.ini", 2904),
        ];

        map["/mnt"] =
        [
            Dir("efs-persist", "/mnt/efs-persist"),
            Dir("efs-system", "/mnt/efs-system"),
        ];

        map["/mnt/efs-persist"] =
        [
            File("device.cfg", "/mnt/efs-persist/device.cfg", 4096),
            File("network.cfg", "/mnt/efs-persist/network.cfg", 1638),
        ];

        map["/mnt/efs-system"] =
        [
            File("installed.txt", "/mnt/efs-system/installed.txt", 15573),
        ];

        map["/net"] =
        [
            File("hosts", "/net/hosts", 221),
            File("resolv.conf", "/net/resolv.conf", 120),
        ];

        map["/tsd"] =
        [
            Dir("persistent", "/tsd/persistent"),
            Dir("persistent1", "/tsd/persistent1"),
            Dir("persistent2", "/tsd/persistent2"),
            Dir("persistent3", "/tsd/persistent3"),
            Dir("persistent4", "/tsd/persistent4"),
            Dir("persistent5", "/tsd/persistent5"),
            Dir("persistent6", "/tsd/persistent6"),
            Dir("persistent7", "/tsd/persistent7"),
            Dir("persistent8", "/tsd/persistent8"),
            File("update.log", "/tsd/update.log", 58122),
            File("update1.log", "/tsd/update1.log", 58122),
            File("update2.log", "/tsd/update2.log", 58122),
            File("update3.log", "/tsd/update3.log", 58122),
            File("update4.log", "/tsd/update4.log", 58122),
            File("update5.log", "/tsd/update5.log", 58122),
            File("update6.log", "/tsd/update6.log", 58122),
            File("update7.log", "/tsd/update7.log", 58122),
            File("update8.log", "/tsd/update8.log", 58122),
            File("update9.log", "/tsd/update9.log", 58122),
            File("update10.log", "/tsd/update10.log", 58122),
            File("update11.log", "/tsd/update11.log", 58122),
            File("update12.log", "/tsd/update12.log", 58122),
            File("update13.log", "/tsd/update13.log", 58122),
            File("update14.log", "/tsd/update14.log", 58122),
            File("update15.log", "/tsd/update15.log", 58122),
        ];

        map["/tsd/persistent"] =
        [
            File("journal.bin", "/tsd/persistent/journal.bin", 1212416),
        ];

        return map;
    }

    private static RemoteExplorerItem Dir(string name, string path)
    {
        return new RemoteExplorerItem
        {
            Name = name,
            FullPath = path,
            EntryType = RemoteEntryType.Directory,
            ModifiedAt = DateTimeOffset.Now.AddDays(-1),
        };
    }

    private static RemoteExplorerItem File(string name, string path, long size)
    {
        return new RemoteExplorerItem
        {
            Name = name,
            FullPath = path,
            EntryType = RemoteEntryType.File,
            Size = size,
            ModifiedAt = DateTimeOffset.Now.AddHours(-6),
        };
    }
}
