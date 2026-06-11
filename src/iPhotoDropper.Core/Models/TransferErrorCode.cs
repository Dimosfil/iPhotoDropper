namespace iPhotoDropper.Core.Models;

public enum TransferErrorCode
{
    None,
    NoDevice,
    DeviceNotTrusted,
    TransferInterrupted,
    DiskFull,
    PermissionDenied,
    UnsupportedFormat,
    Unknown
}
