using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;
using System.Runtime.Versioning;

namespace iPhotoDropper.Infrastructure.Services;

[SupportedOSPlatform("windows7.0")]
public sealed class HybridPhotoLibraryService : IPhotoLibraryService
{
    private readonly IPhoneMtpPhotoLibraryService _mtpLibrary;
    private readonly MockPhotoLibraryService _mockLibrary;

    public HybridPhotoLibraryService(IPhoneMtpPhotoLibraryService mtpLibrary, MockPhotoLibraryService mockLibrary)
    {
        _mtpLibrary = mtpLibrary;
        _mockLibrary = mockLibrary;
    }

    public Task<IReadOnlyList<MediaItem>> ScanMediaAsync(
        DeviceInfo device,
        CancellationToken cancellationToken = default,
        IProgress<MediaItem>? itemDiscovered = null)
    {
        return IPhoneMtpDeviceService.IsMtpDevice(device)
            ? _mtpLibrary.ScanMediaAsync(device, cancellationToken, itemDiscovered)
            : _mockLibrary.ScanMediaAsync(device, cancellationToken, itemDiscovered);
    }

    public Task<Stream> OpenMediaStreamAsync(DeviceInfo device, MediaItem mediaItem, CancellationToken cancellationToken = default)
    {
        return IPhoneMtpDeviceService.IsMtpDevice(device)
            ? _mtpLibrary.OpenMediaStreamAsync(device, mediaItem, cancellationToken)
            : _mockLibrary.OpenMediaStreamAsync(device, mediaItem, cancellationToken);
    }
}
