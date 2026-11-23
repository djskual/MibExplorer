using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using MibExplorer.Models.Adaptations;

namespace MibExplorer.Services.Adaptations;

public sealed class AdaptationCatalogService
{
    private const string ResourceSuffix = "Data.Adaptations.adaptations_catalog.json";

    public AdaptationCatalog Load()
    {
        Assembly assembly = typeof(AdaptationCatalogService).Assembly;

        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name =>
                name.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            string available = string.Join(Environment.NewLine, assembly.GetManifestResourceNames());

            throw new FileNotFoundException(
                $"Adaptations catalog embedded resource not found. Expected suffix: {ResourceSuffix}{Environment.NewLine}{available}");
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
            throw new FileNotFoundException($"Unable to open embedded resource: {resourceName}");

        AdaptationCatalog? catalog = JsonSerializer.Deserialize<AdaptationCatalog>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (catalog is null)
            throw new InvalidOperationException("Unable to deserialize adaptations catalog.");

        return catalog;
    }

    // Catalog selection is strictly ASAM + ODX version prefix. No VIN/model guesses.
    // ODX contains diagnostic definitions, not RCC persistence addresses.
    public AdaptationCatalog? LoadForAsam(string asamIdentifier, string odxVersion)
    {
        if (string.IsNullOrWhiteSpace(asamIdentifier) ||
            string.IsNullOrWhiteSpace(odxVersion) || odxVersion.Length < 3)
            return null;

        Assembly assembly = typeof(AdaptationCatalogService).Assembly;
        using Stream indexStream = OpenEmbedded(assembly, "Data.Adaptations.Odx.index.json");
        using JsonDocument index = JsonDocument.Parse(indexStream);
        List<JsonElement> candidates = index.RootElement.GetProperty("variants")
            .EnumerateArray()
            .Where(v => string.Equals(v.GetProperty("identifier").GetString(), asamIdentifier, StringComparison.Ordinal)
                && odxVersion.StartsWith(v.GetProperty("versionPrefix").GetString() ?? string.Empty, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
            return null;
        if (candidates.Count != 1)
            throw new InvalidDataException("Ambiguous ODX variant for ASAM identifier/version.");

        JsonElement selected = candidates[0];
        string file = selected.GetProperty("resourceFile").GetString()
            ?? throw new InvalidDataException("Missing ODX resource filename.");
        using Stream compressed = OpenEmbedded(assembly, $"Data.Adaptations.Odx.{file}.gz");
        using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
        AdaptationCatalog catalog = JsonSerializer.Deserialize<AdaptationCatalog>(decompressed)
            ?? throw new InvalidDataException("Unable to decode selected ODX catalog.");

        if (!string.Equals(catalog.Ecu, asamIdentifier, StringComparison.Ordinal) ||
            !string.Equals(catalog.Name, selected.GetProperty("variant").GetString(), StringComparison.Ordinal) ||
            catalog.Groups.Count != selected.GetProperty("didCount").GetInt32())
            throw new InvalidDataException("ODX catalog/index mismatch.");

        return catalog;
    }

    private static Stream OpenEmbedded(Assembly assembly, string suffix)
    {
        string? resource = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        return resource is null
            ? throw new FileNotFoundException($"Embedded ODX resource not found: {suffix}")
            : assembly.GetManifestResourceStream(resource)
                ?? throw new FileNotFoundException($"Unable to open ODX resource: {resource}");
    }

}