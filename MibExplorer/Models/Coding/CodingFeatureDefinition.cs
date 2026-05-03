namespace MibExplorer.Models.Coding;

public sealed class CodingCatalog
{
    public int SchemaVersion { get; set; }
    public string Ecu { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<CodingFeatureDefinition> Features { get; set; } = new();
}

public sealed class CodingFeatureDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Byte { get; set; }
    public int Mask { get; set; }
    public int Shift { get; set; }
    public int BitLength { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = "int";
    public string Control { get; set; } = string.Empty;
    public string CurrentRaw { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public List<CodingFeatureOption> Values { get; set; } = new();
}

public sealed class CodingFeatureOption
{
    public string Raw { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public override string ToString()
    {
        return Label;
    }
}