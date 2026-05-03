using MibExplorer.Models.Coding;
using MibExplorer.Services.Coding;

namespace MibExplorer.Services.Design;

public sealed class DesignCodingCenterService : ICodingCenterService
{
    public async Task<CodingReadResult> Read5FCodingAsync(
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(700, cancellationToken);

        const string codingHex = "02731001FF0000004111000100091A221F0109E471A00000BF";
        const string vin = "DESIGNWVWZZZAUZ0001";

        var bytes = new List<CodingByte>();

        for (int i = 0; i < codingHex.Length; i += 2)
        {
            bytes.Add(new CodingByte
            {
                Index = i / 2,
                Value = Convert.ToByte(codingHex.Substring(i, 2), 16)
            });
        }

        onOutput?.Invoke("=== MibExplorer Coding Center Design Mode ===");
        onOutput?.Invoke($"MIBEXPLORER_CODING_HEX={codingHex}");
        onOutput?.Invoke($"MIBEXPLORER_BYTE_COUNT={bytes.Count}");
        onOutput?.Invoke($"MIBEXPLORER_VIN={vin}");

        return new CodingReadResult
        {
            CodingHex = codingHex,
            ByteCount = bytes.Count,
            Vin = vin,
            Bytes = bytes
        };
    }

    public Task<CodingWriteResult> Write5FCodingAsync(
    string targetHex,
    Action<string>? onOutput = null,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        onOutput?.Invoke("=== MibExplorer Coding Center Write Design Mode ===");
        onOutput?.Invoke($"MIBEXPLORER_BEFORE_HEX={targetHex}");
        onOutput?.Invoke("MIBEXPLORER_WRITE_RC=0");
        onOutput?.Invoke("MIBEXPLORER_FLUSH_RC=0");
        onOutput?.Invoke($"MIBEXPLORER_AFTER_HEX={targetHex}");
        onOutput?.Invoke("MIBEXPLORER_WRITE_RESULT=OK");

        return Task.FromResult(new CodingWriteResult
        {
            BeforeHex = targetHex,
            AfterHex = targetHex,
            Success = true,
            Message = "Design mode write simulated."
        });
    }
}