using MibExplorer.Models.Scripting;
using MibExplorer.Services.Scripting;
using MibExplorer.Settings;
using System.IO;

namespace MibExplorer.Services.Design;

public sealed class DesignScriptCatalogService : IScriptCatalogService
{
    private sealed record ScriptHeaderInfo(string ScriptType, string Version, string Author, string Description); 
    public string ScriptsFolderPath => ResolveScriptsFolderPath();
    public string OfficialScriptsFolderPath => Path.Combine(ScriptsFolderPath, "Official");
    public string CustomScriptsFolderPath => Path.Combine(ScriptsFolderPath, "Custom");

    private static string ResolveScriptsFolderPath()
    {
        var configuredPath = AppSettingsStore.Current.ScriptsFolderPath;

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "Scripts");
    }

    public void EnsureScriptsFolderExists()
    {
        var root = ScriptsFolderPath;
        var official = OfficialScriptsFolderPath;
        var custom = CustomScriptsFolderPath;

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(official);
        Directory.CreateDirectory(custom);

        CreateIfMissing(Path.Combine(official, "dump_variant.sh"),
@"#!/bin/sh
# Type: ReadOnly
# Version: 1.0.0
# Author: MibExplorer
# Dump current variant information
# Design mode sample script
echo ""[Design] dump_variant""
echo ""Reading variant...""
echo ""Done""");

        string packageRoot = Path.Combine(custom, "PackageDemo");
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(Path.Combine(packageRoot, "scripts"));
        Directory.CreateDirectory(Path.Combine(packageRoot, "data"));

        CreateIfMissing(Path.Combine(packageRoot, "run.sh"),
@"#!/bin/sh
# Type: Apply
# Version: 1.0.0
# Author: MibExplorer
# Design package demo
# Demonstrates package execution in design mode
echo ""[Design] package demo start""
sh ./scripts/helper.sh
echo ""Data file contents:""
cat ./data/sample.txt
echo ""[Design] package demo end""");

        CreateIfMissing(Path.Combine(packageRoot, "scripts", "helper.sh"),
@"#!/bin/sh
echo ""[Design] helper script executed""");

        CreateIfMissing(Path.Combine(packageRoot, "data", "sample.txt"),
@"hello from package data");
    }

    private static void CreateIfMissing(string path, string content)
    {
        if (File.Exists(path))
            return;

        File.WriteAllText(path, content);
    }

    public IReadOnlyList<ScriptItem> GetScripts()
    {
        EnsureScriptsFolderExists();

        string officialScript = Path.Combine(OfficialScriptsFolderPath, "dump_variant.sh");
        string customPackageRun = Path.Combine(CustomScriptsFolderPath, "PackageDemo", "run.sh");

        return new List<ScriptItem>
        {
            new ScriptItem
            {
                Name = "dump_variant",
                FileName = "dump_variant.sh",
                LocalPath = officialScript,
                RelativePath = Path.Combine("Official", "dump_variant.sh"),
                Description = ReadHeaderInfo(officialScript).Description,
                ScriptType = ReadHeaderInfo(officialScript).ScriptType,
                Version = ReadHeaderInfo(officialScript).Version,
                Author = ReadHeaderInfo(officialScript).Author,
                IsPackage = false,
                PackageRootPath = string.Empty,
                IsOfficial = true
            },
            new ScriptItem
            {
                Name = "PackageDemo",
                FileName = "run.sh",
                LocalPath = customPackageRun,
                RelativePath = Path.Combine("Custom", "PackageDemo", "run.sh"),
                Description = ReadHeaderInfo(customPackageRun).Description,
                ScriptType = ReadHeaderInfo(customPackageRun).ScriptType,
                Version = ReadHeaderInfo(customPackageRun).Version,
                Author = ReadHeaderInfo(officialScript).Author,
                IsPackage = true,
                PackageRootPath = Path.Combine(CustomScriptsFolderPath, "PackageDemo"),
                IsOfficial = false
            }
        };
    }

    private static ScriptHeaderInfo ReadHeaderInfo(string path)
    {
        try
        {
            var commentLines = new List<string>();

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (commentLines.Count > 0)
                        continue;

                    continue;
                }

                if (line.StartsWith("#!"))
                    continue;

                if (line.StartsWith("#"))
                {
                    var text = line.TrimStart('#').Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        commentLines.Add(text);

                        if (commentLines.Count >= 6)
                            break;

                        continue;
                    }
                }

                break;
            }

            if (commentLines.Count == 0)
                return new ScriptHeaderInfo("Unknown", string.Empty, string.Empty, string.Empty);

            string scriptType = "Unknown";
            string version = string.Empty;
            string author = string.Empty;

            foreach (var line in commentLines)
            {
                if (line.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
                {
                    var parsedType = line.Substring("Type:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(parsedType))
                        scriptType = parsedType;
                }
                else if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                {
                    var parsedVersion = line.Substring("Version:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(parsedVersion))
                        version = parsedVersion;
                }
                else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
                {
                    var parsedAuthor = line.Substring("Author:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(parsedAuthor))
                        author = parsedAuthor;
                }
            }

            var descriptionLines = commentLines
                .Where(l => !l.StartsWith("Type:", StringComparison.OrdinalIgnoreCase)
                         && !l.StartsWith("Version:", StringComparison.OrdinalIgnoreCase)
                         && !l.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
                .Take(3);

            string description = string.Join(Environment.NewLine, descriptionLines);

            return new ScriptHeaderInfo(scriptType, version, author, description);
        }
        catch
        {
            return new ScriptHeaderInfo("Unknown", string.Empty, string.Empty, string.Empty);
        }
    }
}