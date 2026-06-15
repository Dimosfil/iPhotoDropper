using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Core.Interfaces;

public interface IPhotoLibraryService
{
    Task<IReadOnlyList<MediaItem>> ScanMediaAsync(
        DeviceInfo device,
        CancellationToken cancellationToken = default,
        IProgress<MediaItem>? itemDiscovered = null);

    Task<Stream> OpenMediaStreamAsync(DeviceInfo device, MediaItem mediaItem, CancellationToken cancellationToken = default);
}
