namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationWritePlan
{
    public List<AdaptationWriteKeyPlan> Keys { get; } = new();

    public bool HasChanges => Keys.Count > 0;
}

public sealed class AdaptationWriteKeyPlan
{
    public string Partition { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;

    public string CurrentRawValue { get; init; } = string.Empty;
    public string NewRawValue { get; init; } = string.Empty;

    public List<AdaptationWriteFieldPlan> Fields { get; } = new();

    public string KeyDisplay => $"{Partition}:{Key}:{Type}";
}

public sealed class AdaptationWriteFieldPlan
{
    public string Label { get; init; } = string.Empty;
    public string CurrentValue { get; init; } = string.Empty;
    public string NewValue { get; init; } = string.Empty;
    public int? Mask { get; init; }
    public int? Shift { get; init; }
}