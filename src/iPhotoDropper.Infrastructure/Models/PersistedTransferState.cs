using System.Text.Json.Serialization;

namespace iPhotoDropper.Infrastructure.Models;

public sealed class PersistedTransferState
{
    public Dictionary<string, PersistedDeviceState> Devices { get; set; } = new(StringComparer.Ordinal);
}

public sealed class PersistedDeviceState
{
    public HashSet<string> ImportedKeys { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, PersistedImportRecord> Imports { get; set; } = new(StringComparer.Ordinal);
}

public sealed class PersistedImportRecord
{
    public string MediaId { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
