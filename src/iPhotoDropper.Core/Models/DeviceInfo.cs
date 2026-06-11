namespace iPhotoDropper.Core.Models;

public sealed class DeviceInfo
{
    public DeviceInfo(string deviceId, string displayName, bool isConnected, bool isTrusted = false)
    {
        DeviceId = deviceId;
        DisplayName = displayName;
        IsConnected = isConnected;
        IsTrusted = isTrusted;
    }

    public string DeviceId { get; }
    public string DisplayName { get; }
    public bool IsConnected { get; }
    public bool IsTrusted { get; }
    public string? SerialNumber { get; init; }
    public string? Manufacturer { get; init; }
    public string? Transport { get; init; } = "USB";

    public string StatusText => IsConnected ? (IsTrusted ? "Доверие получено" : "Нужен trust") : "Отключено";
}
