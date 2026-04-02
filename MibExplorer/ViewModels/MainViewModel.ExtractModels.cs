using MibExplorer.Models;
using System.Text.Json.Serialization;

namespace MibExplorer.ViewModels;

public sealed partial class MainViewModel
{
    private sealed class ExtractDirectoryPlan
    {
        public string LocalRelativePath { get; init; } = string.Empty;
        public string RemoteRelativePath { get; init; } = string.Empty;
    }

    private sealed class ExtractFilePlan
    {
        public string RemotePath { get; init; } = string.Empty;
        public string LocalRelativePath { get; init; } = string.Empty;
        public string RemoteRelativePath { get; init; } = string.Empty;
        public ulong Size { get; init; }
    }

    private sealed class ExtractPlan
    {
        public List<ExtractDirectoryPlan> Directories { get; } = [];
        public List<ExtractFilePlan> Files { get; } = [];
        public ulong TotalBytes { get; set; }
    }

    private sealed class ExtractMapFile
    {
        [JsonPropertyName("version")]
        public int Version { get; init; } = 1;

        [JsonPropertyName("remoteRoot")]
        public string RemoteRoot { get; init; } = string.Empty;

        [JsonPropertyName("entries")]
        public List<ExtractMapEntry> Entries { get; init; } = [];
    }

    private sealed class ExtractMapEntry
    {
        [JsonPropertyName("localRelativePath")]
        public string LocalRelativePath { get; init; } = string.Empty;

        [JsonPropertyName("remoteRelativePath")]
        public string RemoteRelativePath { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }

    private static string GetExtractRootFolderName(RemoteExplorerItem folder)
    {
        return folder.FullPath == "/"
            ? "root"
            : folder.Name;
    }
}
