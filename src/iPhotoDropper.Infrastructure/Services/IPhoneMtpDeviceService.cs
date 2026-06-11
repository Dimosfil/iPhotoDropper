using iPhotoDropper.Core.Events;
using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;
using MediaDevices;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace iPhotoDropper.Infrastructure.Services;

[SupportedOSPlatform("windows7.0")]
public sealed class IPhoneMtpDeviceService : IUsbDeviceService
{
    public const string DeviceIdPrefix = "mtp:";
    internal static readonly SemaphoreSlim DeviceAccessLock = new(1, 1);

    private readonly ILogger<IPhoneMtpDeviceService> _logger;
    private IReadOnlyList<DeviceInfo> _lastDevices = Array.Empty<DeviceInfo>();

    public IPhoneMtpDeviceService(ILogger<IPhoneMtpDeviceService> logger)
    {
        _logger = logger;
    }

    public event EventHandler<DeviceConnectionEventArgs>? DeviceConnected;
    public event EventHandler<DeviceConnectionEventArgs>? DeviceDisconnected;

    public async Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync(CancellationToken cancellationToken = default)
    {
        await DeviceAccessLock.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(FindConnectedDevices, cancellationToken);
        }
        finally
        {
            DeviceAccessLock.Release();
        }
    }

    public async Task<bool> IsDeviceTrustedAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        var current = await GetConnectedDevicesAsync(cancellationToken);
        return current.Any(x => x.DeviceId == device.DeviceId && x.IsTrusted);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _lastDevices = await GetConnectedDevicesAsync(cancellationToken);
        foreach (var device in _lastDevices)
        {
            DeviceConnected?.Invoke(this, new DeviceConnectionEventArgs(device));
        }
    }

    public Task StopAsync()
    {
        foreach (var device in _lastDevices)
        {
            DeviceDisconnected?.Invoke(this, new DeviceConnectionEventArgs(
                new DeviceInfo(device.DeviceId, device.DisplayName, false, device.IsTrusted)
                {
                    Manufacturer = device.Manufacturer,
                    SerialNumber = device.SerialNumber,
                    Transport = device.Transport
                }));
        }

        _lastDevices = Array.Empty<DeviceInfo>();
        return Task.CompletedTask;
    }

    public static string ToMtpDeviceId(string rawDeviceId)
    {
        return DeviceIdPrefix + rawDeviceId;
    }

    public static string FromMtpDeviceId(string deviceId)
    {
        return deviceId.StartsWith(DeviceIdPrefix, StringComparison.Ordinal)
            ? deviceId[DeviceIdPrefix.Length..]
            : deviceId;
    }

    public static bool IsMtpDevice(DeviceInfo device)
    {
        return device.DeviceId.StartsWith(DeviceIdPrefix, StringComparison.Ordinal);
    }

    private IReadOnlyList<DeviceInfo> FindConnectedDevices()
    {
        var devices = new List<DeviceInfo>();
        foreach (var mediaDevice in MediaDevice.GetDevices())
        {
            try
            {
                if (!LooksLikeApplePhone(mediaDevice))
                {
                    continue;
                }

                var deviceId = SafeRead(() => mediaDevice.DeviceId) ?? Guid.NewGuid().ToString("N");
                var trusted = CanReadMediaRoot(mediaDevice);
                devices.Add(new DeviceInfo(ToMtpDeviceId(deviceId), BuildName(mediaDevice), isConnected: true, trusted)
                {
                    Manufacturer = SafeRead(() => mediaDevice.Manufacturer),
                    SerialNumber = SafeRead(() => mediaDevice.SerialNumber),
                    Transport = "USB/MTP"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect MTP device {Device}", SafeRead(() => mediaDevice.FriendlyName) ?? "unknown");
            }
            finally
            {
                SafeDisconnect(mediaDevice);
            }
        }

        return devices;
    }

    private static bool LooksLikeApplePhone(MediaDevice device)
    {
        var text = string.Join(" ",
            SafeRead(() => device.FriendlyName),
            SafeRead(() => device.Description),
            SafeRead(() => device.Manufacturer),
            SafeRead(() => device.Model),
            SafeRead(() => device.PnPDeviceID));
        return text.Contains("iphone", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ipad", StringComparison.OrdinalIgnoreCase)
            || text.Contains("apple", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildName(MediaDevice device)
    {
        var friendlyName = SafeRead(() => device.FriendlyName);
        if (!string.IsNullOrWhiteSpace(friendlyName))
        {
            return friendlyName;
        }

        var model = SafeRead(() => device.Model);
        if (!string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        return "iPhone (USB/MTP)";
    }

    private static bool CanReadMediaRoot(MediaDevice device)
    {
        try
        {
            device.Connect(MediaDeviceAccess.GenericRead, MediaDeviceShare.Read, false);
            return IPhoneMtpPhotoLibraryService.TryFindMediaRoot(device, out _);
        }
        catch
        {
            return false;
        }
    }

    private static void SafeDisconnect(MediaDevice device)
    {
        try
        {
            if (device.IsConnected)
            {
                device.Disconnect();
            }
        }
        catch
        {
            // Best-effort MTP cleanup.
        }
    }

    private static string? SafeRead(Func<string?> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }
}
