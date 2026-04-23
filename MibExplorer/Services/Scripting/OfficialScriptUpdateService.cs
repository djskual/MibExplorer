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
            return "Official scripts are already up to date.";
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
                continue;
            }

            if (!string.Equals(local.Sha256, remote.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.PackagesToDownload.Add(remote);
            }
        }

        // Single scripts to download/update
        foreach (var remote in remoteSingles.Values)
        {
            if (!localSingles.TryGetValue(remote.Name, out var local))
            {
                result.SingleScriptsToDownload.Add(remote);
                continue;
            }

            if (!string.Equals(local.Sha256, remote.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.SingleScriptsToDownload.Add(remote);
            }
        }

        // Packages to delete locally
        foreach (var local in localPackages.Values)
        {
            if (!remotePackages.ContainsKey(local.Name))
            {
                result.PackageNamesToDelete.Add(local.Name);
            }
        }

        // Single scripts to delete locally
        foreach (var local in localSingles.Values)
        {
            if (!remoteSingles.ContainsKey(local.Name))
            {
                result.SingleScriptNamesToDelete.Add(local.Name);
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
        int addedOrUpdated =
            comparison.PackagesToDownload.Count +
            comparison.SingleScriptsToDownload.Count;

        int removed =
            comparison.PackageNamesToDelete.Count +
            comparison.SingleScriptNamesToDelete.Count;

        if (removed == 0)
            return $"Official scripts updated ({addedOrUpdated} item(s)).";

        return $"Official scripts synchronized ({addedOrUpdated} updated, {removed} removed).";
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
        public List<OfficialScriptEntry> PackagesToDownload { get; } = new();

        public List<OfficialScriptEntry> SingleScriptsToDownload { get; } = new();

        public List<string> PackageNamesToDelete { get; } = new();

        public List<string> SingleScriptNamesToDelete { get; } = new();

        public bool HasChanges { get; set; }
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
}