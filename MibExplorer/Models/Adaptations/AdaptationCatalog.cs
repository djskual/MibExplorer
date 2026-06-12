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

    [JsonPropertyName("entries")]
    public List<AdaptationCatalogStorageEntry> Entries { get; init; } = new();

    [JsonPropertyName("readPolicy")]
    public string? ReadPolicy { get; init; }

    [JsonPropertyName("displayPolicy")]
    public string? DisplayPolicy { get; init; }

    [JsonPropertyName("writePolicy")]
    public string? WritePolicy { get; init; }

    [JsonPropertyName("mismatchPolicy")]
    public string? MismatchPolicy { get; init; }

    [JsonPropertyName("mask")]
    public int? Mask { get; init; }

    [JsonPropertyName("shift")]
    public int? Shift { get; init; }

    [JsonPropertyName("bitWidth")]
    public int? BitWidth { get; init; }

    [JsonPropertyName("byteIndex")]
    public int? ByteIndex { get; init; }

    [JsonPropertyName("bitIndex")]
    public int? BitIndex { get; init; }

    [JsonPropertyName("values")]
    public List<AdaptationCatalogEnumValue> Values { get; init; } = new();

    [JsonPropertyName("confidence")]
    public string? Confidence { get; init; }

    [JsonIgnore]
    public bool IsMultiStorage =>
        string.Equals(Mode, "multiStorage", StringComparison.OrdinalIgnoreCase)
        || Entries.Count > 0;

    [JsonIgnore]
    public IReadOnlyList<PhysicalStorageKey> PhysicalKeys
    {
        get
        {
            if (IsMultiStorage)
            {
                return Entries
                    .Where(e =>
                        !string.IsNullOrWhiteSpace(e.Partition) &&
                        !string.IsNullOrWhiteSpace(e.Key) &&
                        !string.IsNullOrWhiteSpace(e.Type))
                    .Select(e => new PhysicalStorageKey(
                        e.Partition!,
                        e.Key!,
                        e.Type!))
                    .Distinct()
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(Partition) &&
                !string.IsNullOrWhiteSpace(Key) &&
                !string.IsNullOrWhiteSpace(Type))
            {
                return new[]
                {
                new PhysicalStorageKey(
                    Partition!,
                    Key!,
                    Type!)
            };
            }

            return Array.Empty<PhysicalStorageKey>();
        }
    }
}

public sealed class AdaptationCatalogStorageEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("partition")]
    public string? Partition { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = string.Empty;

    [JsonPropertyName("mask")]
    public int? Mask { get; init; }

    [JsonPropertyName("shift")]
    public int? Shift { get; init; }

    [JsonPropertyName("bitWidth")]
    public int? BitWidth { get; init; }

    [JsonPropertyName("byteIndex")]
    public int? ByteIndex { get; init; }

    [JsonPropertyName("bitIndex")]
    public int? BitIndex { get; init; }

    [JsonPropertyName("values")]
    public List<AdaptationCatalogEnumValue> Values { get; init; } = new();

    [JsonPropertyName("confidence")]
    public string? Confidence { get; init; }
}
