using System.IO;
using System.Text;
using System.Text.Json;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
    private async Task WriteExtractMapAsync(string extractRoot, string remoteRootPath, ExtractPlan plan)
    {
        string mapPath = Path.Combine(extractRoot, ".mibexplorer-map.json");

        string remoteRootNormalized = NormalizeRemotePathForMap(remoteRootPath);

        var entries = new List<ExtractMapEntry>();

        bool hasDifferences = false;

        foreach (ExtractDirectoryPlan directory in plan.Directories.OrderBy(p => p.LocalRelativePath, StringComparer.Ordinal))
        {
            string local = NormalizeRelativePathForMap(directory.LocalRelativePath);
            string remote = NormalizeRelativePathForMap(directory.RemoteRelativePath);

            if (!string.Equals(local, remote, StringComparison.Ordinal))
                hasDifferences = true;

            entries.Add(new ExtractMapEntry
            {
                LocalRelativePath = local,
                RemoteRelativePath = remote,
                Type = "directory"
            });
        }

        foreach (ExtractFilePlan file in plan.Files.OrderBy(f => f.LocalRelativePath, StringComparer.Ordinal))
        {
            string local = NormalizeRelativePathForMap(file.LocalRelativePath);
            string remote = NormalizeRelativePathForMap(file.RemoteRelativePath);

            if (!string.Equals(local, remote, StringComparison.Ordinal))
                hasDifferences = true;

            entries.Add(new ExtractMapEntry
            {
                LocalRelativePath = local,
                RemoteRelativePath = remote,
                Type = "file"
            });
        }

        // Si aucune différence → on ne cree PAS le fichier
        if (!hasDifferences)
            return;

        var map = new ExtractMapFile
        {
            RemoteRoot = remoteRootNormalized,
            Entries = entries
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(map, jsonOptions);
        await File.WriteAllTextAsync(mapPath, json);
    }

    private async Task<ExtractMapFile?> TryReadExtractMapAsync(string localRoot)
    {
        string mapPath = Path.Combine(localRoot, ".mibexplorer-map.json");

        if (!File.Exists(mapPath))
            return null;

        try
        {
            string json = await File.ReadAllTextAsync(mapPath);

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<ExtractMapFile>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGetMappedRootFolderNameAsync(string localRoot)
    {
        ExtractMapFile? map = await TryReadExtractMapAsync(localRoot);

        if (map is null || string.IsNullOrWhiteSpace(map.RemoteRoot))
            return null;

        string normalizedRemoteRoot = NormalizeRemotePathForMap(map.RemoteRoot);

        if (normalizedRemoteRoot == "/")
            return null;

        int lastSlashIndex = normalizedRemoteRoot.LastIndexOf('/');

        return lastSlashIndex >= 0
            ? normalizedRemoteRoot[(lastSlashIndex + 1)..]
            : normalizedRemoteRoot;
    }

    private async Task<Dictionary<string, ExtractMapEntry>> LoadUploadReplayEntriesAsync(string localRoot)
    {
        ExtractMapFile? map = await TryReadExtractMapAsync(localRoot);

        if (map?.Entries is null || map.Entries.Count == 0)
            return new Dictionary<string, ExtractMapEntry>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, ExtractMapEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (ExtractMapEntry entry in map.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.LocalRelativePath) ||
                string.IsNullOrWhiteSpace(entry.RemoteRelativePath))
            {
                continue;
            }

            string localRelativePath = NormalizeRelativePathForMap(entry.LocalRelativePath);
            result[localRelativePath] = entry;
        }

        return result;
    }

    private static string ResolveUploadRemoteRelativePath(
        string localRelativePath,
        IReadOnlyDictionary<string, ExtractMapEntry> replayEntries)
    {
        string normalizedLocalRelativePath = NormalizeRelativePathForMap(localRelativePath);

        if (replayEntries.TryGetValue(normalizedLocalRelativePath, out ExtractMapEntry? entry) &&
            !string.IsNullOrWhiteSpace(entry.RemoteRelativePath))
        {
            return NormalizeRelativePathForMap(entry.RemoteRelativePath);
        }

        return normalizedLocalRelativePath;
    }

    private static string NormalizeRemotePathForMap(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static string NormalizeRelativePathForMap(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string SanitizeLocalPathSegment(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "_";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);

        foreach (char c in name)
        {
            builder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        }

        string sanitized = builder.ToString().Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "_";

        sanitized = sanitized.TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "_";

        string upper = sanitized.ToUpperInvariant();

        if (upper is "CON" or "PRN" or "AUX" or "NUL" or
            "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or
            "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9")
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    private static string CombineRelativePath(string parent, string child)
    {
        if (string.IsNullOrWhiteSpace(parent))
            return child;

        return Path.Combine(parent, child);
    }
}
