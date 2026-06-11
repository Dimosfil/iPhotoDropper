using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Core.Events;

public sealed class DeviceConnectionEventArgs : EventArgs
{
    public DeviceConnectionEventArgs(DeviceInfo deviceInfo)
    {
        DeviceInfo = deviceInfo;
    }

    public DeviceInfo DeviceInfo { get; }
}
