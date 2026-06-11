using System.Text.Json.Serialization;

namespace iPhotoDropper.Core.Models;

public sealed class MediaItem
{
    public string DeviceId { get; init; } = string.Empty;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FileName { get; init; } = string.Empty;
    public MediaKind Kind { get; init; } = MediaKind.Photo;
    public long SizeBytes { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
    public string? RelativePath { get; init; }
    public string? MimeType { get; init; }

    [JsonIgnore]
    public string? SourcePath { get; init; }
}
