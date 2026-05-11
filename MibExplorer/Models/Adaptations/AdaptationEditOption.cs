namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationEditOption
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;

    public override string ToString() => Label;
}