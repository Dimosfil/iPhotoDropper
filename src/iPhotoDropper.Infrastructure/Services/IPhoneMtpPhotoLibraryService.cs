using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;
using MediaDevices;
using System.Runtime.Versioning;

namespace iPhotoDropper.Infrastructure.Services;

[SupportedOSPlatform("windows7.0")]
public sealed class IPhoneMtpPhotoLibraryService : IPhotoLibraryService
{
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".tiff", ".bmp", ".gif", ".webp", ".dng"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".avi"
    };

    public Task<IReadOnlyList<MediaItem>> ScanMediaAsync(DeviceInfo device, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            await IPhoneMtpDeviceService.DeviceAccessLock.WaitAsync(cancellationToken);
            try
            {
                using var mediaDevice = OpenDevice(device);
                if (!TryFindMediaRoot(mediaDevice, out var mediaRoot))
                {
                    return (IReadOnlyList<MediaItem>)Array.Empty<MediaItem>();
                }

                var files = mediaDevice
                    .EnumerateFiles(mediaRoot, "*.*", SearchOption.AllDirectories)
                    .Where(IsSupported)
                    .Take(5000);

                var items = new List<MediaItem>();
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var info = mediaDevice.GetFileInfo(file);
                    var fileName = SafeRead(() => info.Name) ?? Path.GetFileName(file);
                    var fullName = SafeRead(() => info.FullName) ?? file;
                    var persistentId = SafeRead(() => info.PersistentUniqueId);
                    var sizeBytes = SafeReadLong(() => checked((long)Math.Min(info.Length, long.MaxValue)));
                    var ext = Path.GetExtension(fileName);
                    var kind = VideoExtensions.Contains(ext) ? MediaKind.Video : MediaKind.Photo;
                    var captured = FirstValidDate(
                        SafeRead(() => info.DateAuthored),
                        SafeRead(() => info.CreationTime),
                        SafeRead(() => info.LastWriteTime));

                    items.Add(new MediaItem
                    {
                        DeviceId = device.DeviceId,
                        Id = !string.IsNullOrWhiteSpace(persistentId) ? persistentId : fullName,
                        FileName = fileName,
                        Kind = kind,
                        SizeBytes = sizeBytes,
                        CapturedAt = captured,
                        RelativePath = fullName,
                        MimeType = GetMimeType(ext),
                        SourcePath = fullName
                    });
                }

                return items
                    .OrderByDescending(x => x.CapturedAt ?? DateTimeOffset.MinValue)
                    .ToArray();
            }
            finally
            {
                IPhoneMtpDeviceService.DeviceAccessLock.Release();
            }
        }, cancellationToken);
    }

    public Task<Stream> OpenMediaStreamAsync(DeviceInfo device, MediaItem mediaItem, CancellationToken cancellationToken = default)
    {
        return Task.Run<Stream>(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await IPhoneMtpDeviceService.DeviceAccessLock.WaitAsync(cancellationToken);
            var mediaDevice = OpenDevice(device);
            try
            {
                var sourcePath = mediaItem.SourcePath ?? mediaItem.RelativePath;
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    throw new FileNotFoundException("У медиа нет MTP-пути.", mediaItem.FileName);
                }

                return new ConnectedMediaStream(mediaDevice, mediaDevice.GetFileInfo(sourcePath).OpenRead(), IPhoneMtpDeviceService.DeviceAccessLock);
            }
            catch
            {
                mediaDevice.Disconnect();
                mediaDevice.Dispose();
                IPhoneMtpDeviceService.DeviceAccessLock.Release();
                throw;
            }
        }, cancellationToken);
    }

    internal static bool TryFindMediaRoot(MediaDevice device, out string mediaRoot)
    {
        var candidates = new[]
        {
            @"\Internal Storage\DCIM",
            @"\Internal Storage",
            @"\Внутреннее хранилище\DCIM",
            @"\Внутреннее хранилище",
            @"\DCIM"
        };

        foreach (var candidate in candidates)
        {
            if (device.DirectoryExists(candidate))
            {
                mediaRoot = candidate;
                return true;
            }
        }

        try
        {
            var rootDirectories = device.GetDirectories(@"\");
            foreach (var root in rootDirectories)
            {
                if (root.Contains("storage", StringComparison.OrdinalIgnoreCase)
                    || root.Contains("хранили", StringComparison.OrdinalIgnoreCase))
                {
                    mediaRoot = root;
                    return true;
                }

                var dcimCandidate = CombineMtpPath(root, "DCIM");
                if (device.DirectoryExists(dcimCandidate))
                {
                    mediaRoot = dcimCandidate;
                    return true;
                }
            }
        }
        catch
        {
            // Some devices reject root enumeration until trust is accepted.
        }

        mediaRoot = string.Empty;
        return false;
    }

    private static MediaDevice OpenDevice(DeviceInfo device)
    {
        var rawDeviceId = IPhoneMtpDeviceService.FromMtpDeviceId(device.DeviceId);
        var mediaDevice = MediaDevice.GetDevices().FirstOrDefault(x => x.DeviceId == rawDeviceId);
        if (mediaDevice is null)
        {
            throw new InvalidOperationException("iPhone не найден через Windows MTP. Проверьте USB и доверие на телефоне.");
        }

        mediaDevice.Connect(MediaDeviceAccess.GenericRead, MediaDeviceShare.Read, false);
        return mediaDevice;
    }

    private static string CombineMtpPath(string left, string right)
    {
        return left.TrimEnd('\\') + "\\" + right.TrimStart('\\');
    }

    private static DateTimeOffset? FirstValidDate(params DateTime?[] dates)
    {
        foreach (var date in dates)
        {
            if (date is null || date.Value.Year < 1900 || date.Value.Year > 9998)
            {
                continue;
            }

            try
            {
                return new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Local));
            }
            catch
            {
                // MTP providers can expose invalid timestamps. Keep the file and omit the date.
            }
        }

        return null;
    }

    private static T? SafeRead<T>(Func<T?> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }

    private static long SafeReadLong(Func<long> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsSupported(string file)
    {
        var ext = Path.GetExtension(file);
        return PhotoExtensions.Contains(ext) || VideoExtensions.Contains(ext);
    }

    private static string GetMimeType(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".heic" or ".heif" => "image/heic",
            ".tiff" or ".tif" => "image/tiff",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".dng" => "image/x-adobe-dng",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".m4v" => "video/x-m4v",
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream"
        };
    }

    private sealed class ConnectedMediaStream : Stream
    {
        private readonly MediaDevice _device;
        private readonly Stream _inner;
        private readonly SemaphoreSlim _accessLock;

        public ConnectedMediaStream(MediaDevice device, Stream inner, SemaphoreSlim accessLock)
        {
            _device = device;
            _inner = inner;
            _accessLock = accessLock;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _device.Disconnect();
                _device.Dispose();
                _accessLock.Release();
            }

            base.Dispose(disposing);
        }
    }
}
