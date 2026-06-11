namespace iPhotoDropper.Core.Models;

public sealed class TransferStateEntry
{
    public string DeviceId { get; init; } = string.Empty;
    public string MediaKey { get; init; } = string.Empty;
    public string MediaId { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
