namespace MibExplorer.Models.Scripting;

public sealed class ScriptItem
{
    public string Name { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string LocalPath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ScriptType { get; init; } = "Unknown";

    public bool IsPackage { get; init; }

    public string PackageRootPath { get; init; } = string.Empty;

    public bool IsReadOnlyType => string.Equals(ScriptType, "ReadOnly", StringComparison.OrdinalIgnoreCase);

    public bool IsApplyType => string.Equals(ScriptType, "Apply", StringComparison.OrdinalIgnoreCase);

    public bool IsDangerousType => string.Equals(ScriptType, "Dangerous", StringComparison.OrdinalIgnoreCase);
}