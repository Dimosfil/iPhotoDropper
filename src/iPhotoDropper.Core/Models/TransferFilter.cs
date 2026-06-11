namespace iPhotoDropper.Core.Models;

public sealed class TransferFilter
{
    public bool IncludePhotos { get; set; } = true;
    public bool IncludeVideos { get; set; } = true;
    public long? MaxFileSizeBytes { get; set; }
    public string? SearchText { get; set; }
}

