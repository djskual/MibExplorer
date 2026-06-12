namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Vin { get; set; } = "UNKNOWN";

    public string Source { get; set; } = string.Empty;

    public string OdisGroup { get; set; } = string.Empty;

    public string OdisGroupDisplay =>
        string.IsNullOrWhiteSpace(OdisGroup) ? "-" : OdisGroup;

    public string PhysicalKey { get; set; } = string.Empty;

    public string OriginalRawValue { get; set; } = string.Empty;

    public string MergedRawValue { get; set; } = string.Empty;

    public string ReadbackRawValue { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public List<AdaptationHistoryChange> Changes { get; set; } = new();

    public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm:ss");

    public string Summary => $"{PhysicalKey} - {Status}";
}

public sealed class AdaptationHistoryChange
{
    public string AdaptationId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string CurrentValue { get; set; } = string.Empty;

    public string NewValue { get; set; } = string.Empty;
}