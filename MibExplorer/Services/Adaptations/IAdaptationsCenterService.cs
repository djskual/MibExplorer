using MibExplorer.Models.Adaptations;

namespace MibExplorer.Services.Adaptations;

public interface IAdaptationsCenterService
{
    Task<IReadOnlyList<AdaptationReadValue>> ReadAdaptationsAsync(
        IReadOnlyCollection<AdaptationDefinition> definitions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);
}