using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Infrastructure.Services;

public sealed class MockPhotoLibraryService : IPhotoLibraryService
{
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".tiff", ".bmp", ".gif", ".webp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".avi", ".mkv", ".webm"
    };

    private readonly string _mockRootPath;

    public MockPhotoLibraryService(string? mockRootPath = null)
    {
        _mockRootPath = string.IsNullOrWhiteSpace(mockRootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "iPhotoDropperMockDevice")
            : mockRootPath;
    }

    public Task<IReadOnlyList<MediaItem>> ScanMediaAsync(
        DeviceInfo device,
        CancellationToken cancellationToken = default,
        IProgress<MediaItem>? itemDiscovered = null)
    {
        var items = new List<MediaItem>();

        if (!Directory.Exists(_mockRootPath))
        {
            return Task.FromResult<IReadOnlyList<MediaItem>>(items);
        }

        var files = Directory.EnumerateFiles(_mockRootPath, "*.*", SearchOption.AllDirectories)
            .Where(file => IsSupported(file))
            .OrderByDescending(file => File.GetLastWriteTimeUtc(file));

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            var ext = info.Extension;
            var kind = VideoExtensions.Contains(ext) ? MediaKind.Video : MediaKind.Photo;
            var relative = TryGetRelativePath(file);
            var item = new MediaItem
            {
                DeviceId = device.DeviceId,
                Id = $"{device.DeviceId}|{relative}|{info.Length}|{info.LastWriteTimeUtc:O}",
                FileName = info.Name,
                Kind = kind,
                SizeBytes = info.Length,
                CapturedAt = info.LastWriteTimeUtc,
                SourceCreatedAt = info.CreationTimeUtc,
                SourceModifiedAt = info.LastWriteTimeUtc,
                RelativePath = relative,
                MimeType = GetMimeType(ext),
                SourcePath = file
            };
            items.Add(item);
            itemDiscovered?.Report(item);
        }

        return Task.FromResult<IReadOnlyList<MediaItem>>(items);
    }

    public Task<Stream> OpenMediaStreamAsync(DeviceInfo device, MediaItem mediaItem, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaItem.SourcePath) || !File.Exists(mediaItem.SourcePath))
        {
            throw new FileNotFoundException("Файл на устройстве недоступен", mediaItem.SourcePath);
        }

        Stream stream = new FileStream(mediaItem.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return Task.FromResult(stream);
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
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".m4v" => "video/x-m4v",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            _ => "application/octet-stream"
        };
    }

    private static bool IsSupported(string file)
    {
        var ext = Path.GetExtension(file);
        return PhotoExtensions.Contains(ext) || VideoExtensions.Contains(ext);
    }

    private string TryGetRelativePath(string file)
    {
        if (!Directory.Exists(_mockRootPath))
        {
            return Path.GetFileName(file);
        }

        var absolute = Path.GetFullPath(file);
        var root = Path.GetFullPath(_mockRootPath);
        if (absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return absolute[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return Path.GetFileName(file);
    }
}
