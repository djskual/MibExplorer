using System.IO;
using System.Text.Json;
using MibExplorer.Models.Adaptations;

namespace MibExplorer.Services.Adaptations;

public sealed class AdaptationHistoryService
{
    private const string HistoryRootFolderName = "AdaptationHistory";
    private const string IndexFileName = "transactions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string GetVehicleHistoryFolder(string vin)
    {
        string safeVin = MakeSafeVehicleId(vin);

        return Path.Combine(
            AppContext.BaseDirectory,
            HistoryRootFolderName,
            safeVin);
    }

    public async Task<List<AdaptationHistoryEntry>> LoadEntriesAsync(string vin)
    {
        string folder = GetVehicleHistoryFolder(vin);
        string indexPath = Path.Combine(folder, IndexFileName);

        if (!File.Exists(indexPath))
            return new List<AdaptationHistoryEntry>();

        string json = await File.ReadAllTextAsync(indexPath);

        return JsonSerializer.Deserialize<List<AdaptationHistoryEntry>>(json, JsonOptions)
               ?? new List<AdaptationHistoryEntry>();
    }

    public async Task SaveEntriesAsync(
        string vin,
        IReadOnlyCollection<AdaptationHistoryEntry> entries)
    {
        string folder = GetVehicleHistoryFolder(vin);
        Directory.CreateDirectory(folder);

        string indexPath = Path.Combine(folder, IndexFileName);
        string tempPath = indexPath + ".tmp";

        string json = JsonSerializer.Serialize(
            entries.OrderByDescending(e => e.CreatedAt).ToList(),
            JsonOptions);

        await File.WriteAllTextAsync(tempPath, json);

        if (File.Exists(indexPath))
            File.Delete(indexPath);

        File.Move(tempPath, indexPath);
    }

    public async Task AddTransactionsAsync(
        string vin,
        string source,
        IReadOnlyCollection<PhysicalWriteTransaction> transactions)
    {
        if (transactions.Count == 0)
            return;

        List<AdaptationHistoryEntry> entries = await LoadEntriesAsync(vin);

        foreach (PhysicalWriteTransaction transaction in transactions)
        {
            entries.Add(new AdaptationHistoryEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.Now,
                Vin = string.IsNullOrWhiteSpace(vin) ? "UNKNOWN" : vin,
                Source = source,
                OdisGroup = BuildOdisGroupLabel(transaction),
                PhysicalKey = transaction.PhysicalKey.ToString(),
                OriginalRawValue = transaction.OriginalRawValue,
                MergedRawValue = transaction.MergedRawValue,
                ReadbackRawValue = transaction.ReadbackRawValue,
                Status = transaction.Status.ToString(),
                Changes = transaction.Changes.Select(change => new AdaptationHistoryChange
                {
                    AdaptationId = change.AdaptationId,
                    Label = change.Label,
                    CurrentValue = change.CurrentDisplayValue,
                    NewValue = change.RequestedDisplayValue
                }).ToList()
            });
        }

        await SaveEntriesAsync(vin, entries);
    }

    private static string BuildOdisGroupLabel(PhysicalWriteTransaction transaction)
    {
        return string.Join(
            ", ",
            transaction.Changes
                .Select(c => c.Adaptation.Group)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string MakeSafeVehicleId(string vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
            return "UNKNOWN";

        string cleaned = new string(vin
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned)
            ? "UNKNOWN"
            : cleaned.ToUpperInvariant();
    }
}