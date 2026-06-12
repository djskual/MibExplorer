namespace MibExplorer.Models.Adaptations;

public sealed class RuntimeStorageValue
{
    public PhysicalStorageKey Key { get; init; } = new(string.Empty, string.Empty, string.Empty);

    public string OriginalRawValue { get; init; } = string.Empty;

    public string RawValue { get; init; } = string.Empty;

    public DateTime ReadAtUtc { get; init; } = DateTime.UtcNow;

    public string Source { get; init; } = string.Empty;

    public bool IsDirty =>
        !string.Equals(OriginalRawValue, RawValue, StringComparison.OrdinalIgnoreCase);
}