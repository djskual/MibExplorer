using System.IO;
using System.Text.Json;
using MibExplorer.Models.Coding;

namespace MibExplorer.Services.Coding;

public sealed class CodingHistoryService
{
    private const string HistoryRootFolderName = "CodingHistory";
    private const string IndexFileName = "snapshots.json";

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

    public async Task<List<CodingHistorySnapshot>> LoadSnapshotsAsync(string vin)
    {
        string folder = GetVehicleHistoryFolder(vin);
        string indexPath = Path.Combine(folder, IndexFileName);

        if (!File.Exists(indexPath))
            return new List<CodingHistorySnapshot>();

        string json = await File.ReadAllTextAsync(indexPath);

        return JsonSerializer.Deserialize<List<CodingHistorySnapshot>>(json, JsonOptions)
               ?? new List<CodingHistorySnapshot>();
    }

    public async Task SaveSnapshotsAsync(string vin, IReadOnlyCollection<CodingHistorySnapshot> snapshots)
    {
        string folder = GetVehicleHistoryFolder(vin);
        Directory.CreateDirectory(folder);

        string indexPath = Path.Combine(folder, IndexFileName);
        string tempPath = indexPath + ".tmp";

        string json = JsonSerializer.Serialize(
            snapshots.OrderByDescending(s => s.CreatedAt).ToList(),
            JsonOptions);

        await File.WriteAllTextAsync(tempPath, json);

        if (File.Exists(indexPath))
            File.Delete(indexPath);

        File.Move(tempPath, indexPath);
    }

    public async Task<CodingHistorySnapshot> AddSnapshotAsync(
        string vin,
        string codingHex,
        int byteCount,
        string reason,
        IReadOnlyCollection<CodingPendingChange> pendingChanges,
        string comment = "")
    {
        List<CodingHistorySnapshot> snapshots = await LoadSnapshotsAsync(vin);

        var snapshot = new CodingHistorySnapshot
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.Now,
            Reason = reason,
            CodingHex = codingHex,
            ByteCount = byteCount,
            ChangesCount = pendingChanges.Count,
            Comment = comment,
            PendingChanges = pendingChanges.Select(change => new CodingPendingChange
            {
                FeatureId = change.FeatureId,
                Label = change.Label,
                CurrentValue = change.CurrentValue,
                CurrentRawValue = change.CurrentRawValue,
                NewValue = change.NewValue,
                NewRawValue = change.NewRawValue,
                Key = change.Key,
                Type = change.Type
            }).ToList()
        };

        snapshots.Add(snapshot);

        await SaveSnapshotsAsync(vin, snapshots);

        return snapshot;
    }

    public async Task DeleteSnapshotAsync(string vin, CodingHistorySnapshot snapshot)
    {
        List<CodingHistorySnapshot> snapshots = await LoadSnapshotsAsync(vin);
        snapshots.RemoveAll(s => string.Equals(s.Id, snapshot.Id, StringComparison.OrdinalIgnoreCase));

        await SaveSnapshotsAsync(vin, snapshots);
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