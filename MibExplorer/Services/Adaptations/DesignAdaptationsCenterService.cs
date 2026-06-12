using MibExplorer.Models.Adaptations;

namespace MibExplorer.Services.Adaptations;

public sealed class DesignAdaptationsCenterService : IAdaptationsCenterService
{
    public Task<string> ReadVinAsync(
    Action<string>? onOutput = null,
    CancellationToken cancellationToken = default)
    {
        const string vin = "DESIGNWVWZZZAUZ0001";

        cancellationToken.ThrowIfCancellationRequested();

        onOutput?.Invoke("=== Adaptations Center design VIN read ===");
        onOutput?.Invoke($"MIBEXPLORER_VIN={vin}");

        return Task.FromResult(vin);
    }

    public Task<IReadOnlyList<AdaptationReadValue>> ReadAdaptationsAsync(
        IReadOnlyCollection<AdaptationDefinition> definitions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        onOutput?.Invoke("=== Adaptations Center design read ===");

        List<AdaptationReadValue> values = definitions
        .SelectMany(d => GetPhysicalKeys(d).Select(k => new
        {
            Definition = d,
            Key = k
        }))
        .GroupBy(x => AdaptationReadValue.BuildCacheKey(
            x.Key.Partition,
            x.Key.Key,
            x.Key.Type))
        .Select(g =>
        {
            AdaptationDefinition first = g.First().Definition;
            PhysicalStorageKey firstKey = g.First().Key;
            List<AdaptationDefinition> groupedDefinitions = g
                .Select(x => x.Definition)
                .ToList();

            string value;

            if (string.Equals(first.StorageMode, "packedFlags", StringComparison.OrdinalIgnoreCase))
            {
                value = BuildPackedFlagsDesignValue(groupedDefinitions);
            }
            else if (groupedDefinitions.Any(d =>
                            string.Equals(d.StorageMode, "blobBit", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(d.StorageMode, "blobBitsEnum", StringComparison.OrdinalIgnoreCase)))
            {
                value = BuildBlobDesignValue(groupedDefinitions);
            }
            else if (string.Equals(first.StorageMode, "scalarEnum", StringComparison.OrdinalIgnoreCase))
            {
                value = TryGetStorageRawValue(first, first.CurrentValueFromDump, out int rawValue)
                    ? rawValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : FirstNonEmpty(first.CurrentValueFromDump, "-");
            }
            else
            {
                value = FirstNonEmpty(first.CurrentValueFromDump, "-");
            }

            return new AdaptationReadValue
            {
                Partition = firstKey.Partition,
                Key = firstKey.Key,
                Type = firstKey.Type,
                Value = value
            };
        })
        .ToList();

        onOutput?.Invoke($"Design values loaded: {values.Count}");

        return Task.FromResult<IReadOnlyList<AdaptationReadValue>>(values);
    }

    public Task<AdaptationWriteResult> PreflightWriteAdaptationsAsync(
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        onOutput?.Invoke("=== Adaptations Center design write preflight ===");

        var result = new AdaptationWriteResult
        {
            Success = true,
            Message = "Design write preflight simulated."
        };

        foreach (PhysicalWriteTransaction transaction in transactions)
        {
            result.PhysicalResults.Add(new AdaptationPhysicalWriteResult
            {
                PhysicalKey = transaction.PhysicalKey,
                ExpectedOriginalRawValue = transaction.OriginalRawValue,
                TargetRawValue = transaction.MergedRawValue,
                ReadbackRawValue = transaction.OriginalRawValue,
                Success = true,
                Message = "Design preflight OK."
            });
        }

        return Task.FromResult(result);
    }

    public Task<AdaptationWriteResult> WriteAdaptationsAsync(
        IReadOnlyCollection<PhysicalWriteTransaction> transactions,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        onOutput?.Invoke("=== Adaptations Center design write ===");

        var result = new AdaptationWriteResult
        {
            Success = true,
            Message = "Design write simulated."
        };

        foreach (PhysicalWriteTransaction transaction in transactions)
        {
            result.PhysicalResults.Add(new AdaptationPhysicalWriteResult
            {
                PhysicalKey = transaction.PhysicalKey,
                ReadbackRawValue = transaction.MergedRawValue,
                Success = true,
                Message = "Design write simulated."
            });
        }

        return Task.FromResult(result);
    }

    private static IEnumerable<PhysicalStorageKey> GetPhysicalKeys(AdaptationDefinition definition)
    {
        if (definition.PhysicalKeys.Count > 0)
            return definition.PhysicalKeys;

        if (!string.IsNullOrWhiteSpace(definition.Persistence.Partition) &&
            !string.IsNullOrWhiteSpace(definition.Persistence.Key) &&
            !string.IsNullOrWhiteSpace(definition.Persistence.Type))
        {
            return new[]
            {
            new PhysicalStorageKey(
                definition.Persistence.Partition,
                definition.Persistence.Key,
                definition.Persistence.Type)
        };
        }

        return Array.Empty<PhysicalStorageKey>();
    }

    private static string BuildPackedFlagsDesignValue(IEnumerable<AdaptationDefinition> definitions)
    {
        int value = 0;

        foreach (AdaptationDefinition definition in definitions)
        {
            if (definition.Mask is not int mask)
                continue;

            if (IsEnabledValue(definition.CurrentValueFromDump))
                value |= mask;
        }

        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildBlobDesignValue(IEnumerable<AdaptationDefinition> definitions)
    {
        List<AdaptationDefinition> mapped = definitions
            .Where(d => d.ByteIndex is int)
            .ToList();

        if (mapped.Count == 0)
            return "-";

        int length = mapped.Max(d => d.ByteIndex!.Value) + 1;
        byte[] bytes = new byte[length];

        foreach (AdaptationDefinition definition in mapped)
        {
            if (definition.ByteIndex is not int byteIndex)
                continue;

            if (string.Equals(definition.StorageMode, "blobBit", StringComparison.OrdinalIgnoreCase))
            {
                if (definition.BitIndex is not int bitIndex || bitIndex < 0 || bitIndex > 7)
                    continue;

                if (IsEnabledValue(definition.CurrentValueFromDump))
                    bytes[byteIndex] |= (byte)(1 << bitIndex);

                continue;
            }

            if (string.Equals(definition.StorageMode, "blobBitsEnum", StringComparison.OrdinalIgnoreCase))
            {
                if (definition.BitIndex is not int bitIndex || definition.BitWidth is not int bitWidth)
                    continue;

                if (!TryGetStorageRawValue(definition, definition.CurrentValueFromDump, out int rawValue))
                    continue;

                TrySetBitRange(bytes, byteIndex, bitIndex, bitWidth, rawValue);
            }
        }

        return string.Join(" ", bytes.Select(b =>
            b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static bool TryGetStorageRawValue(
        AdaptationDefinition definition,
        string value,
        out int rawValue)
    {
        rawValue = 0;

        foreach (AdaptationCatalogEnumValue option in definition.StorageValues)
        {
            if (string.Equals(option.Label, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Raw, value, StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(
                    option.Raw,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out rawValue);
            }
        }

        return int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out rawValue);
    }

    private static bool TrySetBitRange(
        byte[] bytes,
        int byteIndex,
        int bitIndex,
        int bitWidth,
        int value)
    {
        if (byteIndex < 0 || bitIndex < 0 || bitIndex > 7 || bitWidth <= 0 || bitWidth > 31)
            return false;

        int maxValue = (1 << bitWidth) - 1;

        if (value < 0 || value > maxValue)
            return false;

        for (int i = 0; i < bitWidth; i++)
        {
            int absoluteBit = bitIndex + i;
            int currentByteIndex = byteIndex + absoluteBit / 8;
            int currentBitIndex = absoluteBit % 8;

            if (currentByteIndex < 0 || currentByteIndex >= bytes.Length)
                return false;

            byte mask = (byte)(1 << currentBitIndex);
            bool bitSet = (value & (1 << i)) != 0;

            bytes[currentByteIndex] = bitSet
                ? (byte)(bytes[currentByteIndex] | mask)
                : (byte)(bytes[currentByteIndex] & ~mask);
        }

        return true;
    }

    private static bool IsEnabledValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("activated", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_activated", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("available", StringComparison.OrdinalIgnoreCase)
            || value.Equals("[VN]_Available", StringComparison.OrdinalIgnoreCase)
            || value.Equals("enabled", StringComparison.OrdinalIgnoreCase);
    }
}