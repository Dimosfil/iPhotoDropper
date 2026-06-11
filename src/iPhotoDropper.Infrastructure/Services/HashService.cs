using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Infrastructure.Services;

public sealed class HashService : IHashService
{
    public string BuildMediaKey(DeviceInfo device, MediaItem item)
    {
        return $"{device.DeviceId}::{item.Id}";
    }

    public string BuildFallbackMediaKey(DeviceInfo device, MediaItem item)
    {
        var datePart = item.CapturedAt?.UtcDateTime.ToString("O") ?? "n/a";
        return $"{device.DeviceId}::{item.FileName}::{item.SizeBytes}::{datePart}";
    }
}
