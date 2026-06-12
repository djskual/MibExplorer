namespace MibExplorer.Models.Adaptations;

public sealed class PendingAdaptationChange
{
    public AdaptationItemView Adaptation { get; init; } = null!;

    public PhysicalStorageKey PrimaryKey { get; init; } = new(string.Empty, string.Empty, string.Empty);

    public string CacheKey { get; init; } = string.Empty;

    public string CurrentDisplayValue { get; init; } = string.Empty;

    public string RequestedDisplayValue { get; init; } = string.Empty;

    public string RequestedRawValue { get; init; } = string.Empty;

    public string StorageMode { get; init; } = string.Empty;

    public bool IsStorageMapped { get; init; }

    public bool IsMultiStorage { get; init; }

    public bool HasStorageWarning { get; init; }

    public string StorageWarning { get; init; } = string.Empty;

    public string Label => Adaptation.Label;

    public string AdaptationId => Adaptation.Id;
}