using iPhotoDropper.Core.Events;
using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;
using Microsoft.Extensions.Logging;

namespace iPhotoDropper.Infrastructure.Services;

public sealed class MockUsbDeviceService : IUsbDeviceService
{
    private readonly ILogger<MockUsbDeviceService> _logger;
    private DeviceInfo _mockDevice;

    private bool _isRunning;

    public MockUsbDeviceService(ILogger<MockUsbDeviceService> logger)
    {
        _logger = logger;
        _mockDevice = new DeviceInfo("mock-iphone-001", "iPhone (USB Mock)", isConnected: false)
        {
            SerialNumber = "MOCK-SERIAL-001",
            Manufacturer = "Apple (mock)",
            Transport = "USB"
        };
    }

    public event EventHandler<DeviceConnectionEventArgs>? DeviceConnected;
    public event EventHandler<DeviceConnectionEventArgs>? DeviceDisconnected;

    public Task<bool> IsDeviceTrustedAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(device.DeviceId == _mockDevice.DeviceId && device.IsConnected && _mockDevice.IsTrusted);
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return _isRunning && _mockDevice.IsConnected ? new[] { _mockDevice } : Array.Empty<DeviceInfo>();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        await Task.Delay(250, cancellationToken);
        _mockDevice = new DeviceInfo(_mockDevice.DeviceId, _mockDevice.DisplayName, true, true)
        {
            SerialNumber = _mockDevice.SerialNumber,
            Manufacturer = _mockDevice.Manufacturer,
            Transport = _mockDevice.Transport
        };
        DeviceConnected?.Invoke(this, new DeviceConnectionEventArgs(_mockDevice));
        _logger.LogInformation("Mock USB device service started: {Device}", _mockDevice.DisplayName);
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        await Task.CompletedTask;
        _mockDevice = new DeviceInfo(_mockDevice.DeviceId, _mockDevice.DisplayName, false, _mockDevice.IsTrusted)
        {
            SerialNumber = _mockDevice.SerialNumber,
            Manufacturer = _mockDevice.Manufacturer,
            Transport = _mockDevice.Transport
        };
        var disconnected = _mockDevice;
        DeviceDisconnected?.Invoke(this, new DeviceConnectionEventArgs(disconnected));
        _logger.LogInformation("Mock USB device service stopped: {Device}", disconnected.DisplayName);
    }
}
