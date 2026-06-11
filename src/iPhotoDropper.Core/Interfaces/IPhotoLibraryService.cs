using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Core.Interfaces;

public interface IPhotoLibraryService
{
    Task<IReadOnlyList<MediaItem>> ScanMediaAsync(DeviceInfo device, CancellationToken cancellationToken = default);
    Task<Stream> OpenMediaStreamAsync(DeviceInfo device, MediaItem mediaItem, CancellationToken cancellationToken = default);
}
