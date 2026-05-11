namespace MibExplorer.Models.Adaptations;

public sealed class AdaptationReadValue
{
    public string Partition { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string CacheKey => BuildCacheKey(Partition, Key, Type);

    public static string BuildCacheKey(string partition, string key, string type)
    {
        return $"{partition.Trim()}|{key.Trim()}|{type.Trim()}".ToUpperInvariant();
    }
}