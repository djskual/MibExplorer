using System.Text.Json.Serialization;

namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("ecu")]
    public string Ecu { get; init; } = string.Empty;

    [JsonPropertyName("group")]
    public string Group { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("odisPath")]
    public string OdisPath { get; init; } = string.Empty;

    [JsonPropertyName("currentValueFromDump")]
    public string CurrentValueFromDump { get; init; } = string.Empty;

    [JsonPropertyName("persistence")]
    public AdaptationPersistence Persistence { get; init; } = new();

    [JsonPropertyName("ui")]
    public AdaptationUi Ui { get; init; } = new();

    [JsonPropertyName("confidence")]
    public string Confidence { get; init; } = string.Empty;

    [JsonPropertyName("safety")]
    public string Safety { get; init; } = string.Empty;

    [JsonPropertyName("writable")]
    public bool Writable { get; init; }

    [JsonPropertyName("notes")]
    public string Notes { get; init; } = string.Empty;
}

public sealed class AdaptationPersistence
{
    [JsonPropertyName("partition")]
    public string Partition { get; init; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}

public sealed class AdaptationUi
{
    [JsonPropertyName("control")]
    public string Control { get; init; } = string.Empty;

    [JsonPropertyName("limits")]
    public string Limits { get; init; } = string.Empty;
}