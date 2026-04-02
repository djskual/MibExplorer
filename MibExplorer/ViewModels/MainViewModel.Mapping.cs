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

        foreach (ExtractDirectoryPlan directory in plan.Directories.OrderBy(p => p.LocalRelativePath, StringComparer.Ordinal))
        {
            entries.Add(new ExtractMapEntry
            {
                LocalRelativePath = NormalizeRelativePathForMap(directory.LocalRelativePath),
                RemoteRelativePath = NormalizeRelativePathForMap(directory.RemoteRelativePath),
                Type = "directory"
            });
        }

        foreach (ExtractFilePlan file in plan.Files.OrderBy(f => f.LocalRelativePath, StringComparer.Ordinal))
        {
            entries.Add(new ExtractMapEntry
            {
                LocalRelativePath = NormalizeRelativePathForMap(file.LocalRelativePath),
                RemoteRelativePath = NormalizeRelativePathForMap(file.RemoteRelativePath),
                Type = "file"
            });
        }

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
