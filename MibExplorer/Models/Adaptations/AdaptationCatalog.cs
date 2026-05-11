using System.Text.Json.Serialization;

namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationCatalog
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = string.Empty;

    [JsonPropertyName("ecu")]
    public string Ecu { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("stage")]
    public string Stage { get; init; } = string.Empty;

    [JsonPropertyName("groups")]
    public List<AdaptationCatalogGroup> Groups { get; init; } = new();

    [JsonIgnore]
    public bool IsV4 => Groups.Count > 0;
}

public sealed class AdaptationCatalogGroup
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("rdid")]
    public string Rdid { get; init; } = string.Empty;

    [JsonPropertyName("defaultVisibleInMibExplorer")]
    public bool DefaultVisibleInMibExplorer { get; init; }

    [JsonPropertyName("adaptationCount")]
    public int AdaptationCount { get; init; }

    [JsonPropertyName("odisoriginalGroupLabel")]
    public string OdisOriginalGroupLabel { get; init; } = string.Empty;

    [JsonPropertyName("odisrawGroupLabel")]
    public string OdisRawGroupLabel { get; init; } = string.Empty;

    [JsonPropertyName("htmlrawGroupLabel")]
    public string HtmlRawGroupLabel { get; init; } = string.Empty;

    [JsonPropertyName("adaptations")]
    public List<AdaptationCatalogItem> Adaptations { get; init; } = new();
}

public sealed class AdaptationCatalogItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("rawLabel")]
    public string RawLabel { get; init; } = string.Empty;

    [JsonPropertyName("htmllabel")]
    public string HtmlLabel { get; init; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("currentValue")]
    public string CurrentValue { get; init; } = string.Empty;

    [JsonPropertyName("currentValueRaw")]
    public string CurrentValueRaw { get; init; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = string.Empty;

    [JsonPropertyName("valueType")]
    public string ValueType { get; init; } = string.Empty;

    [JsonPropertyName("ui")]
    public AdaptationCatalogItemUi Ui { get; init; } = new();

    [JsonPropertyName("storage")]
    public AdaptationCatalogStorage Storage { get; init; } = new();
}

public sealed class AdaptationCatalogItemUi
{
    [JsonPropertyName("visibleByDefault")]
    public bool VisibleByDefault { get; init; } = true;

    [JsonPropertyName("editable")]
    public bool Editable { get; init; }

    [JsonPropertyName("editor")]
    public string Editor { get; init; } = string.Empty;

    [JsonPropertyName("values")]
    public List<AdaptationCatalogEnumValue> Values { get; init; } = new();

    [JsonPropertyName("min")]
    public double? Min { get; init; }

    [JsonPropertyName("max")]
    public double? Max { get; init; }

    [JsonPropertyName("byteLength")]
    public int? ByteLength { get; init; }

    [JsonPropertyName("dangerous")]
    public bool Dangerous { get; init; }

    [JsonPropertyName("limitedWriteCount")]
    public bool LimitedWriteCount { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

public sealed class AdaptationCatalogEnumValue
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("raw")]
    public string? Raw { get; init; }
}

public sealed class AdaptationCatalogStorage
{
    [JsonPropertyName("mapped")]
    public bool Mapped { get; init; }

    [JsonPropertyName("mappingSource")]
    public string? MappingSource { get; init; }

    [JsonPropertyName("partition")]
    public string? Partition { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = string.Empty;
}
