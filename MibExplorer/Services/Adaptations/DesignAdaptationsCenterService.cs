using MibExplorer.Models.Adaptations;

namespace MibExplorer.Services.Adaptations;

public sealed class DesignAdaptationsCenterService : IAdaptationsCenterService
{
    public Task<IReadOnlyList<AdaptationReadValue>> ReadAdaptationsAsync(
        IReadOnlyCollection<AdaptationDefinition> definitions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        onOutput?.Invoke("=== Adaptations Center design read ===");

        List<AdaptationReadValue> values = definitions
            .Where(d => !string.IsNullOrWhiteSpace(d.Persistence.Key))
            .GroupBy(d => AdaptationReadValue.BuildCacheKey(
                d.Persistence.Partition,
                d.Persistence.Key,
                d.Persistence.Type))
            .Select(g =>
            {
                AdaptationDefinition first = g.First();

                return new AdaptationReadValue
                {
                    Partition = first.Persistence.Partition,
                    Key = first.Persistence.Key,
                    Type = first.Persistence.Type,
                    Value = string.IsNullOrWhiteSpace(first.CurrentValueFromDump)
                        ? "-"
                        : first.CurrentValueFromDump
                };
            })
            .ToList();

        onOutput?.Invoke($"Design values loaded: {values.Count}");

        return Task.FromResult<IReadOnlyList<AdaptationReadValue>>(values);
    }
}