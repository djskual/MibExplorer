using MibExplorer.Models.Scripting;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MibExplorer.Services.Scripting;

public sealed class OfficialScriptUpdateService : IOfficialScriptUpdateService
{
    private const string RawBaseUrl = "https://raw.githubusercontent.com/djskual/MibExplorer/master/Scripts/Official";
    private const string ApiBaseUrl = "https://api.github.com/repos/djskual/MibExplorer/contents/Scripts/Official";
    private const string ManifestFileName = "manifest.json";

    private readonly HttpClient _httpClient;

    public OfficialScriptUpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MibExplorer/0.4.4");
    }

    public OfficialScriptsManifest? LoadLocalManifest(string officialScriptsFolderPath)
    {
        try
        {
            string manifestPath = Path.Combine(officialScriptsFolderPath, ManifestFileName);
            if (!File.Exists(manifestPath))
                return null;

            string json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<OfficialScriptsManifest>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CheckForOfficialUpdatesAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(officialScriptsFolderPath);

        var localManifest = LoadLocalManifest(officialScriptsFolderPath);
        var remoteManifest = await LoadRemoteManifestAsync(cancellationToken);

        var comparison = CompareManifests(localManifest, remoteManifest);
        return comparison.HasChanges;
    }

    public async Task<string> UpdateOfficialScriptsAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(officialScriptsFolderPath);

        var localManifest = LoadLocalManifest(officialScriptsFolderPath);
        var remoteManifest = await LoadRemoteManifestAsync(cancellationToken);

        var comparison = CompareManifests(localManifest, remoteManifest);

        if (!comparison.HasChanges)
        {
            return BuildUpdateSummary(comparison);
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "MibExplorer_OfficialScripts_Update");
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);

        Directory.CreateDirectory(tempRoot);

        try
        {
            // Keep local manifest copy
            string remoteManifestJson = JsonSerializer.Serialize(remoteManifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(
                Path.Combine(tempRoot, ManifestFileName),
                remoteManifestJson,
                cancellationToken);

            // Download updated / added single scripts
            foreach (var script in comparison.SingleScriptsToDownload)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string scriptUrl = $"{RawBaseUrl}/{script.Name}";
                string localPath = Path.Combine(tempRoot, script.Name);

                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                using var response = await _httpClient.GetAsync(scriptUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(localPath);
                await input.CopyToAsync(output, cancellationToken);
            }

            // Download updated / added packages
            foreach (var package in comparison.PackagesToDownload)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string packageTempPath = Path.Combine(tempRoot, package.Name);
                Directory.CreateDirectory(packageTempPath);

                await DownloadGitHubDirectoryRecursiveAsync(
                    $"{ApiBaseUrl}/{package.Name}",
                    packageTempPath,
                    cancellationToken);
            }

            // Mirror local Official directory
            ApplyMirrorChanges(officialScriptsFolderPath, tempRoot, comparison);

            return BuildUpdateSummary(comparison);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    public async Task<string> RestoreOfficialScriptAsync(
        string officialScriptsFolderPath,
        ScriptItem script,
        CancellationToken cancellationToken = default)
    {
        if (!script.IsOfficial)
            return "Selected script is not an Official script.";

        Directory.CreateDirectory(officialScriptsFolderPath);

        var remoteManifest = await LoadRemoteManifestAsync(cancellationToken);

        OfficialScriptEntry? entry;

        if (script.IsPackage)
        {
            entry = remoteManifest.PackagesScripts
                .FirstOrDefault(item => string.Equals(item.Name, script.Name, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                return $"Official package not found in remote manifest: {script.Name}";

            string tempRoot = Path.Combine(Path.GetTempPath(), "MibExplorer_OfficialScript_Restore");
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);

            Directory.CreateDirectory(tempRoot);

            try
            {
                string packageTempPath = Path.Combine(tempRoot, entry.Name);
                Directory.CreateDirectory(packageTempPath);

                await DownloadGitHubDirectoryRecursiveAsync(
                    $"{ApiBaseUrl}/{entry.Name}",
                    packageTempPath,
                    cancellationToken);

                string localPackagePath = Path.Combine(officialScriptsFolderPath, entry.Name);

                if (Directory.Exists(localPackagePath))
                    Directory.Delete(localPackagePath, recursive: true);

                Directory.Move(packageTempPath, localPackagePath);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }

            await SaveManifestAsync(officialScriptsFolderPath, remoteManifest, cancellationToken);

            return $"Official package restored: {script.Name}";
        }

        entry = remoteManifest.SingleScripts
            .FirstOrDefault(item => string.Equals(item.Name, script.FileName, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return $"Official script not found in remote manifest: {script.FileName}";

        string scriptUrl = $"{RawBaseUrl}/{entry.Name}";
        string localScriptPath = Path.Combine(officialScriptsFolderPath, entry.Name);

        using var response = await _httpClient.GetAsync(scriptUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = File.Create(localScriptPath))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        await SaveManifestAsync(officialScriptsFolderPath, remoteManifest, cancellationToken);

        return $"Official script restored: {script.FileName}";
    }

    private static async Task SaveManifestAsync(
        string officialScriptsFolderPath,
        OfficialScriptsManifest manifest,
        CancellationToken cancellationToken)
    {
        string manifestPath = Path.Combine(officialScriptsFolderPath, ManifestFileName);

        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }

    private async Task<OfficialScriptsManifest> LoadRemoteManifestAsync(CancellationToken cancellationToken)
    {
        string manifestUrl = $"{RawBaseUrl}/{ManifestFileName}";
        string manifestJson = await _httpClient.GetStringAsync(manifestUrl, cancellationToken);

        var manifest = JsonSerializer.Deserialize<OfficialScriptsManifest>(
            manifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest is null)
            throw new InvalidOperationException("Failed to parse official scripts manifest.");

        return manifest;
    }

    private static ManifestComparisonResult CompareManifests(
        OfficialScriptsManifest? localManifest,
        OfficialScriptsManifest remoteManifest)
    {
        var result = new ManifestComparisonResult();

        var localPackages = (localManifest?.PackagesScripts ?? new List<OfficialScriptEntry>())
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var remotePackages = remoteManifest.PackagesScripts
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var localSingles = (localManifest?.SingleScripts ?? new List<OfficialScriptEntry>())
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var remoteSingles = remoteManifest.SingleScripts
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        // Packages to download/update
        foreach (var remote in remotePackages.Values)
        {
            if (!localPackages.TryGetValue(remote.Name, out var local))
            {
                result.PackagesToDownload.Add(remote);
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "ADDED",
                    Kind = "Package",
                    Name = remote.Name,
                    NewVersion = remote.Version
                });
                continue;
            }

            if (!string.Equals(local.Sha256, remote.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.PackagesToDownload.Add(remote);
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "UPDATED",
                    Kind = "Package",
                    Name = remote.Name,
                    OldVersion = local.Version,
                    NewVersion = remote.Version
                });
            }
            else
            {
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "UNCHANGED",
                    Kind = "Package",
                    Name = remote.Name,
                    OldVersion = local.Version,
                    NewVersion = remote.Version
                });
            }
        }

        // Single scripts to download/update
        foreach (var remote in remoteSingles.Values)
        {
            if (!localSingles.TryGetValue(remote.Name, out var local))
            {
                result.SingleScriptsToDownload.Add(remote);
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "ADDED",
                    Kind = "Single",
                    Name = remote.Name,
                    NewVersion = remote.Version
                });
                continue;
            }

            if (!string.Equals(local.Sha256, remote.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.SingleScriptsToDownload.Add(remote);
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "UPDATED",
                    Kind = "Single",
                    Name = remote.Name,
                    OldVersion = local.Version,
                    NewVersion = remote.Version
                });
            }
            else
            {
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "UNCHANGED",
                    Kind = "Single",
                    Name = remote.Name,
                    OldVersion = local.Version,
                    NewVersion = remote.Version
                });
            }
        }

        // Packages to delete locally
        foreach (var local in localPackages.Values)
        {
            if (!remotePackages.ContainsKey(local.Name))
            {
                result.PackageNamesToDelete.Add(local.Name);
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "REMOVED",
                    Kind = "Package",
                    Name = local.Name,
                    OldVersion = local.Version
                });
            }
        }

        // Single scripts to delete locally
        foreach (var local in localSingles.Values)
        {
            if (!remoteSingles.ContainsKey(local.Name))
            {
                result.SingleScriptNamesToDelete.Add(local.Name);
                result.Changes.Add(new OfficialScriptChange
                {
                    Action = "REMOVED",
                    Kind = "Single",
                    Name = local.Name,
                    OldVersion = local.Version
                });
            }
        }

        result.HasChanges =
            result.PackagesToDownload.Count > 0 ||
            result.SingleScriptsToDownload.Count > 0 ||
            result.PackageNamesToDelete.Count > 0 ||
            result.SingleScriptNamesToDelete.Count > 0 ||
            localManifest is null;

        return result;
    }

    private static void ApplyMirrorChanges(
        string officialScriptsFolderPath,
        string tempRoot,
        ManifestComparisonResult comparison)
    {
        Directory.CreateDirectory(officialScriptsFolderPath);

        // Delete removed packages
        foreach (string packageName in comparison.PackageNamesToDelete)
        {
            string localPath = Path.Combine(officialScriptsFolderPath, packageName);
            if (Directory.Exists(localPath))
                Directory.Delete(localPath, recursive: true);
        }

        // Delete removed single scripts
        foreach (string scriptName in comparison.SingleScriptNamesToDelete)
        {
            string localPath = Path.Combine(officialScriptsFolderPath, scriptName);
            if (File.Exists(localPath))
                File.Delete(localPath);
        }

        // Replace / add downloaded packages
        foreach (var package in comparison.PackagesToDownload)
        {
            string localPath = Path.Combine(officialScriptsFolderPath, package.Name);
            string tempPath = Path.Combine(tempRoot, package.Name);

            if (Directory.Exists(localPath))
                Directory.Delete(localPath, recursive: true);

            if (Directory.Exists(tempPath))
                Directory.Move(tempPath, localPath);
        }

        // Replace / add downloaded single scripts
        foreach (var script in comparison.SingleScriptsToDownload)
        {
            string localPath = Path.Combine(officialScriptsFolderPath, script.Name);
            string tempPath = Path.Combine(tempRoot, script.Name);

            if (File.Exists(localPath))
                File.Delete(localPath);

            if (File.Exists(tempPath))
                File.Move(tempPath, localPath);
        }

        // Always update local manifest
        string localManifestPath = Path.Combine(officialScriptsFolderPath, ManifestFileName);
        string tempManifestPath = Path.Combine(tempRoot, ManifestFileName);

        if (File.Exists(localManifestPath))
            File.Delete(localManifestPath);

        if (File.Exists(tempManifestPath))
            File.Move(tempManifestPath, localManifestPath);
    }

    private static string BuildUpdateSummary(ManifestComparisonResult comparison)
    {
        var lines = new List<string>();

        int added = comparison.Changes.Count(change => change.Action == "ADDED");
        int updated = comparison.Changes.Count(change => change.Action == "UPDATED");
        int removed = comparison.Changes.Count(change => change.Action == "REMOVED");
        int unchanged = comparison.Changes.Count(change => change.Action == "UNCHANGED");

        lines.Add("Official scripts synchronized.");
        lines.Add($"Added: {added}, Updated: {updated}, Removed: {removed}, Unchanged: {unchanged}");
        lines.Add(string.Empty);

        foreach (var change in comparison.Changes
                     .OrderBy(change => change.Kind, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(change => change.Name, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add(FormatChange(change));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatChange(OfficialScriptChange change)
    {
        return change.Action switch
        {
            "ADDED" => $"[ADDED] {change.Name} {FormatVersion(change.NewVersion)}",
            "UPDATED" => $"[UPDATED] {change.Name} {FormatVersion(change.OldVersion)} -> {FormatVersion(change.NewVersion)}",
            "REMOVED" => $"[REMOVED] {change.Name} {FormatVersion(change.OldVersion)}",
            "UNCHANGED" => $"[UNCHANGED] {change.Name} {FormatVersion(change.NewVersion ?? change.OldVersion)}",
            _ => $"[{change.Action}] {change.Name}"
        };
    }

    private static string FormatVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? "v-" : $"v{version}";
    }

    private async Task DownloadGitHubDirectoryRecursiveAsync(string apiUrl, string localFolder, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        var items = JsonSerializer.Deserialize<List<GitHubContentItem>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (items is null)
            throw new InvalidOperationException($"Failed to parse GitHub directory listing: {apiUrl}");

        Directory.CreateDirectory(localFolder);

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(item.Type, "file", StringComparison.OrdinalIgnoreCase))
            {
                string localPath = Path.Combine(localFolder, item.Name);

                using var fileResponse = await _httpClient.GetAsync(item.DownloadUrl, cancellationToken);
                fileResponse.EnsureSuccessStatusCode();

                await using var input = await fileResponse.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(localPath);
                await input.CopyToAsync(output, cancellationToken);
            }
            else if (string.Equals(item.Type, "dir", StringComparison.OrdinalIgnoreCase))
            {
                string childLocalFolder = Path.Combine(localFolder, item.Name);
                await DownloadGitHubDirectoryRecursiveAsync(item.Url, childLocalFolder, cancellationToken);
            }
        }
    }

    private sealed class ManifestComparisonResult
    {
        public List<OfficialScriptChange> Changes { get; } = new();

        public List<OfficialScriptEntry> PackagesToDownload { get; } = new();

        public List<OfficialScriptEntry> SingleScriptsToDownload { get; } = new();

        public List<string> PackageNamesToDelete { get; } = new();

        public List<string> SingleScriptNamesToDelete { get; } = new();

        public bool HasChanges { get; set; }
    }

    private sealed class OfficialScriptChange
    {
        public string Action { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string? OldVersion { get; init; }

        public string? NewVersion { get; init; }
    }

    private sealed class GitHubContentItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public string? GetExpectedSha256(string officialScriptsFolderPath, ScriptItem script)
    {
        if (!script.IsOfficial)
            return null;

        var manifest = LoadLocalManifest(officialScriptsFolderPath);
        if (manifest is null)
            return null;

        if (script.IsPackage)
        {
            return manifest.PackagesScripts
                .FirstOrDefault(entry => string.Equals(entry.Name, script.Name, StringComparison.OrdinalIgnoreCase))
                ?.Sha256;
        }

        return manifest.SingleScripts
            .FirstOrDefault(entry => string.Equals(entry.Name, script.FileName, StringComparison.OrdinalIgnoreCase))
            ?.Sha256;
    }
}