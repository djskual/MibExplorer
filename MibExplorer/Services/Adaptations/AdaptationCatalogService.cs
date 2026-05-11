using System.IO;
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
}