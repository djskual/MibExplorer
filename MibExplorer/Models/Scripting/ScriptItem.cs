using MibExplorer.Core;

namespace MibExplorer.Models.Scripting;

public sealed class ScriptItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string LocalPath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ScriptType { get; init; } = "Unknown";

    public string Version { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    public bool IsPackage { get; init; }

    public string PackageRootPath { get; init; } = string.Empty;

    public bool IsOfficial { get; init; }

    private bool _isModified;
    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    public string VersionDisplay =>
        string.IsNullOrWhiteSpace(Version) ? "Version: -" : $"Version: {Version}";

    public string AuthorDisplay =>
        string.IsNullOrWhiteSpace(Author) ? "Author: -" : $"Author: {Author}";

    public string ScriptTooltip =>
        string.Join(Environment.NewLine,
            Name,
            ScriptType,
            VersionDisplay,
            AuthorDisplay);

    public bool IsReadOnlyType => string.Equals(ScriptType, "ReadOnly", StringComparison.OrdinalIgnoreCase);

    public bool IsApplyType => string.Equals(ScriptType, "Apply", StringComparison.OrdinalIgnoreCase);

    public bool IsDangerousType => string.Equals(ScriptType, "Dangerous", StringComparison.OrdinalIgnoreCase);
}