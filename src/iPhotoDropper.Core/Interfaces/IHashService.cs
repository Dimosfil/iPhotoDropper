using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Core.Interfaces;

public interface IHashService
{
    string BuildMediaKey(DeviceInfo device, MediaItem item);
    string BuildFallbackMediaKey(DeviceInfo device, MediaItem item);
}
