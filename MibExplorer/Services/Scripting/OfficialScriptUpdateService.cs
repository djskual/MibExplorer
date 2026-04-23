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

    private readonly HttpClient _httpClient;

    public OfficialScriptUpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MibExplorer/0.4.4");
    }

    public async Task<string> UpdateOfficialScriptsAsync(string officialScriptsFolderPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(officialScriptsFolderPath);

        string manifestUrl = $"{RawBaseUrl}/manifest.json";
        string manifestJson = await _httpClient.GetStringAsync(manifestUrl, cancellationToken);

        var manifest = JsonSerializer.Deserialize<OfficialScriptsManifest>(
            manifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest is null)
            throw new InvalidOperationException("Failed to parse official scripts manifest.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "MibExplorer_OfficialScripts_Update");
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);

        Directory.CreateDirectory(tempRoot);

        await File.WriteAllTextAsync(
            Path.Combine(tempRoot, "manifest.json"),
            manifestJson,
            cancellationToken);

        try
        {
            // Download single scripts
            foreach (string scriptName in manifest.SingleScripts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string scriptUrl = $"{RawBaseUrl}/{scriptName}";
                string localPath = Path.Combine(tempRoot, scriptName);

                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                using var response = await _httpClient.GetAsync(scriptUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(localPath);
                await input.CopyToAsync(output, cancellationToken);
            }

            // Download package scripts recursively
            foreach (string packageName in manifest.PackagesScripts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string packageTempPath = Path.Combine(tempRoot, packageName);
                Directory.CreateDirectory(packageTempPath);

                await DownloadGitHubDirectoryRecursiveAsync(
                    $"{ApiBaseUrl}/{packageName}",
                    packageTempPath,
                    cancellationToken);
            }

            // Replace local Official folder contents
            foreach (string entry in Directory.EnumerateFileSystemEntries(officialScriptsFolderPath))
            {
                string name = Path.GetFileName(entry);

                if (Directory.Exists(entry))
                    Directory.Delete(entry, recursive: true);
                else
                    File.Delete(entry);
            }

            foreach (string dir in Directory.EnumerateDirectories(tempRoot))
            {
                string name = Path.GetFileName(dir);
                string dest = Path.Combine(officialScriptsFolderPath, name);
                Directory.Move(dir, dest);
            }

            foreach (string file in Directory.EnumerateFiles(tempRoot))
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(officialScriptsFolderPath, name);
                File.Move(file, dest, overwrite: true);
            }

            int totalCount = manifest.PackagesScripts.Count + manifest.SingleScripts.Count;
            return totalCount == 0
                ? "No official scripts listed in manifest."
                : $"Official scripts updated ({totalCount} item(s)).";
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
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