using System.IO;
using System.Reflection;
using System.Text.Json;
using MibExplorer.Models.Coding;

namespace MibExplorer.Services.Coding;

public sealed class CodingFeatureService
{
    private const string CatalogResourceSuffix =
        "Data.Coding.mib2_5f_coding_catalog.json";

    private List<CodingFeatureDefinition> _definitions = new();

    public async Task LoadAsync(string path)
    {
        string json = await ReadCatalogJsonAsync(path);

        CodingCatalog? catalog = JsonSerializer.Deserialize<CodingCatalog>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        _definitions = catalog?.Features ?? new();
    }

    public List<CodingFeatureValue> Decode(IReadOnlyList<CodingByte> bytes)
    {
        var result = new List<CodingFeatureValue>();

        foreach (CodingFeatureDefinition def in _definitions)
        {
            string rawValue = DecodeRawValue(def, bytes);

            CodingFeatureOption? option = def.Values.FirstOrDefault(v =>
                string.Equals(v.Raw, rawValue, StringComparison.OrdinalIgnoreCase));

            string value = option?.Label
                ?? (string.IsNullOrWhiteSpace(rawValue)
                    ? "Not read"
                    : $"Unknown ({rawValue})");

            var feature = new CodingFeatureValue
            {
                Id = def.Id,
                Label = def.Label,
                Value = value,
                RawValue = rawValue,
                Key = def.Key,
                Type = def.Type,
                Byte = def.Byte,
                Mask = def.Mask,
                Shift = def.Shift,
                BitLength = def.BitLength
            };

            foreach (CodingFeatureOption valueOption in def.Values
                         .OrderBy(v => int.TryParse(v.Raw, out int n) ? n : int.MaxValue)
                         .ThenBy(v => v.Raw))
            {
                if (!feature.Options.Any(o =>
                        string.Equals(o.Raw, valueOption.Raw, StringComparison.OrdinalIgnoreCase)))
                {
                    feature.Options.Add(valueOption);
                }
            }

            if (!string.IsNullOrWhiteSpace(feature.RawValue) &&
                !feature.Options.Any(o =>
                    string.Equals(o.Raw, feature.RawValue, StringComparison.OrdinalIgnoreCase)))
            {
                feature.Options.Add(new CodingFeatureOption
                {
                    Raw = feature.RawValue,
                    Label = value
                });
            }

            result.Add(feature);
        }

        return result;
    }

    public List<CodingByte> BuildModifiedBytes(
    IReadOnlyList<CodingByte> currentBytes,
    IEnumerable<CodingFeatureValue> features)
    {
        byte[] values = currentBytes
            .OrderBy(b => b.Index)
            .Select(b => b.Value)
            .ToArray();

        foreach (CodingFeatureValue feature in features.Where(f => f.IsModified))
        {
            if (feature.Byte < 0 || feature.Byte >= values.Length)
                continue;

            if (!int.TryParse(feature.SelectedRawValue, out int selectedRawValue))
                continue;

            int mask = feature.Mask & 0xFF;
            int currentByteValue = values[feature.Byte];

            int clearedValue = currentByteValue & ~mask;
            int shiftedValue = (selectedRawValue << feature.Shift) & mask;
            int newByteValue = clearedValue | shiftedValue;

            values[feature.Byte] = (byte)newByteValue;
        }

        var modifiedBytes = new List<CodingByte>();

        for (int i = 0; i < values.Length; i++)
        {
            modifiedBytes.Add(new CodingByte
            {
                Index = i,
                Value = values[i]
            });
        }

        return modifiedBytes;
    }

    public string BuildCodingHex(IReadOnlyList<CodingByte> bytes)
    {
        return string.Concat(bytes
            .OrderBy(b => b.Index)
            .Select(b => b.Value.ToString("X2")));
    }

    private static async Task<string> ReadCatalogJsonAsync(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return await File.ReadAllTextAsync(path);

        Assembly assembly = Assembly.GetExecutingAssembly();

        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name =>
                name.EndsWith(CatalogResourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            string available = string.Join(
                Environment.NewLine,
                assembly.GetManifestResourceNames());

            throw new FileNotFoundException(
                $"Embedded coding catalog not found. Expected suffix: {CatalogResourceSuffix}{Environment.NewLine}{available}");
        }

        await using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
            throw new FileNotFoundException($"Unable to open embedded catalog: {resourceName}");

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static string DecodeRawValue(
        CodingFeatureDefinition definition,
        IReadOnlyList<CodingByte> bytes)
    {
        if (definition.Byte < 0 || definition.Byte >= bytes.Count)
            return string.Empty;

        int byteValue = bytes[definition.Byte].Value;
        int maskedValue = byteValue & definition.Mask;
        int rawValue = definition.Shift > 0
            ? maskedValue >> definition.Shift
            : maskedValue;

        return rawValue.ToString();
    }
}