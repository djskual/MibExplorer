using MibExplorer.Models.Coding;

namespace MibExplorer.Services.Coding;

public interface ICodingCenterService
{
    Task<CodingReadResult> Read5FCodingAsync(
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);

    Task<CodingWriteResult> Write5FCodingAsync(
        string targetHex,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);
}