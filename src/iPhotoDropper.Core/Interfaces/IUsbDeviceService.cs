using iPhotoDropper.Core.Events;
using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Core.Interfaces;

public interface IUsbDeviceService
{
    Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync(CancellationToken cancellationToken = default);
    Task<bool> IsDeviceTrustedAsync(DeviceInfo device, CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();

    event EventHandler<DeviceConnectionEventArgs>? DeviceConnected;
    event EventHandler<DeviceConnectionEventArgs>? DeviceDisconnected;
}
