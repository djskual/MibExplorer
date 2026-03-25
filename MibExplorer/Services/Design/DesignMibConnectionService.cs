using MibExplorer.Models;

namespace MibExplorer.Services.Design;

public sealed class DesignMibConnectionService : IMibConnectionService
{
    private readonly Dictionary<string, IReadOnlyList<RemoteExplorerItem>> _map;

    public DesignMibConnectionService()
    {
        _map = BuildMap();
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
            File("update.log", "/tsd/update.log", 58122),
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
