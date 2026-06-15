using System.Runtime.Versioning;
using iPhotoDropper.Core.Events;
using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Infrastructure.Services;

[SupportedOSPlatform("windows7.0")]
public sealed class HybridUsbDeviceService : IUsbDeviceService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IPhoneMtpDeviceService _mtpService;
    private readonly MockUsbDeviceService _mockService;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private IReadOnlyList<DeviceInfo> _currentDevices = Array.Empty<DeviceInfo>();
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private bool _realDeviceSeen;
    private bool _mockStarted;

    public HybridUsbDeviceService(IPhoneMtpDeviceService mtpService, MockUsbDeviceService mockService)
    {
        _mtpService = mtpService;
        _mockService = mockService;
    }

    public event EventHandler<DeviceConnectionEventArgs>? DeviceConnected;
    public event EventHandler<DeviceConnectionEventArgs>? DeviceDisconnected;

    public async Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            return _currentDevices.ToArray();
        }
        finally
        {
            _sync.Release();
        }
    }

    public Task<bool> IsDeviceTrustedAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        return IPhoneMtpDeviceService.IsMtpDevice(device)
            ? _mtpService.IsDeviceTrustedAsync(device, cancellationToken)
            : _mockService.IsDeviceTrustedAsync(device, cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_pollCts is not null)
        {
            return;
        }

        await RefreshAndPublishAsync(cancellationToken);
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollTask = PollAsync(_pollCts.Token);
    }

    public async Task StopAsync()
    {
        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync();
        }

        if (_pollTask is not null)
        {
            try
            {
                await _pollTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _pollCts?.Dispose();
        _pollCts = null;
        _pollTask = null;

        await PublishDiffAsync(Array.Empty<DeviceInfo>());
        if (_mockStarted)
        {
            await _mockService.StopAsync();
            _mockStarted = false;
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshAndPublishAsync(cancellationToken);
        }
    }

    private async Task RefreshAndPublishAsync(CancellationToken cancellationToken)
    {
        var mtpDevices = await _mtpService.GetConnectedDevicesAsync(cancellationToken);
        IReadOnlyList<DeviceInfo> desiredDevices;

        if (mtpDevices.Count > 0)
        {
            _realDeviceSeen = true;
            desiredDevices = mtpDevices;

            if (_mockStarted)
            {
                await _mockService.StopAsync();
                _mockStarted = false;
            }
        }
        else if (_realDeviceSeen)
        {
            desiredDevices = Array.Empty<DeviceInfo>();
        }
        else
        {
            if (!_mockStarted)
            {
                await _mockService.StartAsync(cancellationToken);
                _mockStarted = true;
            }

            desiredDevices = await _mockService.GetConnectedDevicesAsync(cancellationToken);
        }

        await PublishDiffAsync(desiredDevices);
    }

    private async Task PublishDiffAsync(IReadOnlyList<DeviceInfo> desiredDevices)
    {
        await _sync.WaitAsync();
        try
        {
            var previous = _currentDevices;
            var previousIds = previous.Select(x => x.DeviceId).ToHashSet(StringComparer.Ordinal);
            var desiredIds = desiredDevices.Select(x => x.DeviceId).ToHashSet(StringComparer.Ordinal);

            foreach (var removed in previous.Where(x => !desiredIds.Contains(x.DeviceId)))
            {
                DeviceDisconnected?.Invoke(this, new DeviceConnectionEventArgs(
                    new DeviceInfo(removed.DeviceId, removed.DisplayName, false, removed.IsTrusted)
                    {
                        Manufacturer = removed.Manufacturer,
                        SerialNumber = removed.SerialNumber,
                        Transport = removed.Transport
                    }));
            }

            foreach (var added in desiredDevices.Where(x => !previousIds.Contains(x.DeviceId)))
            {
                DeviceConnected?.Invoke(this, new DeviceConnectionEventArgs(added));
            }

            foreach (var changed in desiredDevices.Where(next =>
                         previous.FirstOrDefault(prev => prev.DeviceId == next.DeviceId) is { } prev
                         && HasDeviceChanged(prev, next)))
            {
                DeviceDisconnected?.Invoke(this, new DeviceConnectionEventArgs(
                    new DeviceInfo(changed.DeviceId, changed.DisplayName, false, changed.IsTrusted)
                    {
                        Manufacturer = changed.Manufacturer,
                        SerialNumber = changed.SerialNumber,
                        Transport = changed.Transport
                    }));
                DeviceConnected?.Invoke(this, new DeviceConnectionEventArgs(changed));
            }

            _currentDevices = desiredDevices.ToArray();
        }
        finally
        {
            _sync.Release();
        }
    }

    private static bool HasDeviceChanged(DeviceInfo previous, DeviceInfo next)
    {
        return previous.IsTrusted != next.IsTrusted
            || !string.Equals(previous.DisplayName, next.DisplayName, StringComparison.Ordinal)
            || !string.Equals(previous.SerialNumber, next.SerialNumber, StringComparison.Ordinal)
            || !string.Equals(previous.Manufacturer, next.Manufacturer, StringComparison.Ordinal)
            || !string.Equals(previous.Transport, next.Transport, StringComparison.Ordinal);
    }
}
