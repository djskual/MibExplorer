using MibExplorer.Models.Adaptations;

namespace MibExplorer.Services.Adaptations;

public interface IAdaptationsCenterService
{
    Task<string> ReadVinAsync(
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdaptationReadValue>> ReadAdaptationsAsync(
        IReadOnlyCollection<AdaptationDefinition> definitions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);

    Task<AdaptationWriteResult> PreflightWriteAdaptationsAsync(
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);

    Task<AdaptationWriteResult> WriteAdaptationsAsync(
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);
}