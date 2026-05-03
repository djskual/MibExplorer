namespace MibExplorer.Models.Coding;

public sealed class CodingPendingChange
{
    public string FeatureId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public string CurrentValue { get; set; } = string.Empty;
    public string CurrentRawValue { get; set; } = string.Empty;

    public string NewValue { get; set; } = string.Empty;
    public string NewRawValue { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public int Byte { get; set; }

    public string CurrentByteHex { get; set; } = string.Empty;

    public string NewByteHex { get; set; } = string.Empty;

    public string ByteLabel => $"Byte {Byte}";
}