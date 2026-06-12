namespace MibExplorer.Models.Adaptations;

public sealed record PhysicalStorageKey(
    string Partition,
    string Key,
    string Type)
{
    public override string ToString()
    {
        return $"{Partition}:{Key}:{Type}";
    }
}