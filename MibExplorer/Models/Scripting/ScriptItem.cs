namespace MibExplorer.Models.Scripting;

public sealed class ScriptItem
{
    public string Name { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string LocalPath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Category { get; init; } = "Custom";

    public bool IsUserScript { get; init; } = true;

    public bool IsRunnable { get; init; } = true;

    public string Extension { get; init; } = string.Empty;

    public bool IsPackage { get; init; }

    public string PackageRootPath { get; init; } = string.Empty;
}