using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;
using iPhotoDropper.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace iPhotoDropper.Tests;

public sealed class TransferServiceSmokeTests
{
    [Fact]
    public void OverallProgressFallsBackToProcessedItemsWhenByteTotalIsUnknown()
    {
        var progress = new TransferProgress
        {
            TotalItems = 4,
            ProcessedItems = 1,
            TotalBytes = 0,
            TotalBytesTransferred = 0
        };

        Assert.Equal(25, progress.OverallPercent);
    }

    [Fact]
    public async Task MockScanAndImportCopiesFilesAtomicallyAndSkipsDuplicates()
    {
        using var workspace = TempWorkspace.Create();
        var deviceRoot = workspace.CreateDirectory("device");
        var destinationRoot = workspace.CreateDirectory("imports");
        var statePath = Path.Combine(workspace.Root, "state", "transfer-state.json");

        var photoBytes = new byte[] { 1, 2, 3, 4, 5 };
        var videoBytes = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
        await File.WriteAllBytesAsync(Path.Combine(deviceRoot, "IMG_0001.jpg"), photoBytes);
        await File.WriteAllBytesAsync(Path.Combine(deviceRoot, "VID_0001.mov"), videoBytes);
        await File.WriteAllTextAsync(Path.Combine(deviceRoot, "notes.txt"), "ignored");

        var library = new MockPhotoLibraryService(deviceRoot);
        var service = CreateTransferService(library, statePath);
        var device = new DeviceInfo("mock-device", "Mock iPhone", isConnected: true, isTrusted: true);

        var scanned = await library.ScanMediaAsync(device);

        Assert.Equal(2, scanned.Count);
        Assert.Contains(scanned, x => x.Kind == MediaKind.Photo);
        Assert.Contains(scanned, x => x.Kind == MediaKind.Video);

        var firstResult = await service.ImportAsync(device, scanned, new TransferOptions
        {
            DestinationFolder = destinationRoot,
            CopyOnlyNew = true,
            IncludePhotos = true,
            IncludeVideos = true,
            OrganizeByDateFolders = false,
            RetryBaseDelayMs = 1
        });

        Assert.Equal(TransferOperationState.Completed, firstResult.State);
        Assert.Equal(2, firstResult.CopiedCount);
        Assert.Equal(0, firstResult.SkippedCount);
        Assert.Equal(0, firstResult.FailedCount);
        Assert.All(firstResult.Items, item =>
        {
            Assert.True(item.Success);
            Assert.True(File.Exists(item.DestinationPath));
        });
        Assert.Empty(Directory.EnumerateFiles(destinationRoot, "*.tmp", SearchOption.AllDirectories));

        var copiedPhoto = firstResult.Items.Single(x => x.FileName == "IMG_0001.jpg").DestinationPath!;
        var copiedVideo = firstResult.Items.Single(x => x.FileName == "VID_0001.mov").DestinationPath!;
        Assert.Equal(photoBytes, await File.ReadAllBytesAsync(copiedPhoto));
        Assert.Equal(videoBytes, await File.ReadAllBytesAsync(copiedVideo));

        var secondResult = await service.ImportAsync(device, scanned, new TransferOptions
        {
            DestinationFolder = destinationRoot,
            CopyOnlyNew = true,
            IncludePhotos = true,
            IncludeVideos = true,
            OrganizeByDateFolders = false,
            RetryBaseDelayMs = 1
        });

        Assert.Equal(0, secondResult.CopiedCount);
        Assert.Equal(2, secondResult.SkippedCount);
        Assert.Equal(0, secondResult.FailedCount);

        var secondDestinationRoot = workspace.CreateDirectory("imports-second");
        var thirdResult = await service.ImportAsync(device, scanned, new TransferOptions
        {
            DestinationFolder = secondDestinationRoot,
            CopyOnlyNew = true,
            IncludePhotos = true,
            IncludeVideos = true,
            OrganizeByDateFolders = false,
            RetryBaseDelayMs = 1
        });

        Assert.Equal(2, thirdResult.CopiedCount);
        Assert.Equal(0, thirdResult.SkippedCount);
        Assert.Equal(0, thirdResult.FailedCount);
        Assert.Equal(2, Directory.EnumerateFiles(secondDestinationRoot, "*.*", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public async Task ExistingDestinationFileCanBeSkippedOrReplaced()
    {
        using var workspace = TempWorkspace.Create();
        var deviceRoot = workspace.CreateDirectory("device");
        var destinationRoot = workspace.CreateDirectory("imports");
        var statePath = Path.Combine(workspace.Root, "state", "transfer-state.json");
        var sourceBytes = new byte[] { 9, 8, 7, 6 };
        var existingBytes = new byte[] { 1, 1, 1 };

        await File.WriteAllBytesAsync(Path.Combine(deviceRoot, "IMG_0001.jpg"), sourceBytes);

        var library = new MockPhotoLibraryService(deviceRoot);
        var service = CreateTransferService(library, statePath);
        var device = new DeviceInfo("mock-device", "Mock iPhone", isConnected: true, isTrusted: true);
        var scanned = await library.ScanMediaAsync(device);

        var firstResult = await service.ImportAsync(device, scanned, new TransferOptions
        {
            DestinationFolder = destinationRoot,
            CopyOnlyNew = false,
            IncludePhotos = true,
            IncludeVideos = true,
            OrganizeByDateFolders = false,
            RetryBaseDelayMs = 1
        });

        var destinationPath = firstResult.Items.Single().DestinationPath!;
        await File.WriteAllBytesAsync(destinationPath, existingBytes);

        var skippedResult = await service.ImportAsync(device, scanned, new TransferOptions
        {
            DestinationFolder = destinationRoot,
            CopyOnlyNew = false,
            IncludePhotos = true,
            IncludeVideos = true,
            OrganizeByDateFolders = false,
            RetryBaseDelayMs = 1,
            DefaultExistingFileAction = ExistingFileAction.Skip
        });

        Assert.Equal(0, skippedResult.CopiedCount);
        Assert.Equal(1, skippedResult.SkippedCount);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(destinationPath));

        var replacedResult = await service.ImportAsync(device, scanned, new TransferOptions
        {
            DestinationFolder = destinationRoot,
            CopyOnlyNew = false,
            IncludePhotos = true,
            IncludeVideos = true,
            OrganizeByDateFolders = false,
            RetryBaseDelayMs = 1,
            ExistingFileConflictHandler = (_, _) => Task.FromResult(ExistingFileAction.Replace)
        });

        Assert.Equal(1, replacedResult.CopiedCount);
        Assert.Equal(0, replacedResult.SkippedCount);
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.Empty(Directory.EnumerateFiles(destinationRoot, "* (1).*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DefaultImportKeepsFilesInSelectedDestinationFolder()
    {
        using var workspace = TempWorkspace.Create();
        var destinationRoot = workspace.CreateDirectory("flat-imports");
        var statePath = Path.Combine(workspace.Root, "state", "transfer-state.json");
        var device = new DeviceInfo("mock-device", "Mock iPhone", isConnected: true, isTrusted: true);
        var item = new MediaItem
        {
            DeviceId = device.DeviceId,
            Id = "dated-photo",
            FileName = "IMG_2026.jpg",
            Kind = MediaKind.Photo,
            SizeBytes = 3,
            CapturedAt = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero)
        };

        var service = CreateTransferService(new SingleItemPhotoLibraryService(item, new byte[] { 1, 2, 3 }), statePath);
        var result = await service.ImportAsync(device, new[] { item }, new TransferOptions
        {
            DestinationFolder = destinationRoot,
            IncludePhotos = true,
            IncludeVideos = true,
            RetryBaseDelayMs = 1
        });

        Assert.Equal(1, result.CopiedCount);
        var copiedPath = result.Items.Single().DestinationPath!;
        Assert.Equal(destinationRoot, Path.GetDirectoryName(copiedPath));
        Assert.Empty(Directory.EnumerateDirectories(destinationRoot));
    }

    [Fact]
    public async Task PauseResumeAndCancelTransitionsDoNotCrashActiveTransfer()
    {
        using var workspace = TempWorkspace.Create();
        var destinationRoot = workspace.CreateDirectory("imports");
        var statePath = Path.Combine(workspace.Root, "state", "transfer-state.json");
        var device = new DeviceInfo("mock-device", "Mock iPhone", isConnected: true, isTrusted: true);
        var item = new MediaItem
        {
            DeviceId = device.DeviceId,
            Id = "slow-video",
            FileName = "slow-video.mov",
            Kind = MediaKind.Video,
            SizeBytes = 4 * 1024 * 1024,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var library = new SlowPhotoLibraryService(item);
        var service = CreateTransferService(library, statePath);

        var importTask = service.ImportAsync(device, new[] { item }, new TransferOptions
        {
            DestinationFolder = destinationRoot,
            IncludePhotos = true,
            IncludeVideos = true,
            OrganizeByDateFolders = false,
            RetryBaseDelayMs = 1
        });

        await WaitUntilAsync(() => service.State == TransferOperationState.Running, TimeSpan.FromSeconds(2));

        service.Pause();
        Assert.True(service.IsPaused);

        service.Resume();
        Assert.Equal(TransferOperationState.Running, service.State);

        service.Cancel();
        var result = await importTask;

        Assert.True(result.WasCanceled);
        Assert.Equal(TransferOperationState.Canceled, result.State);
        Assert.Empty(Directory.EnumerateFiles(destinationRoot, "*.tmp", SearchOption.AllDirectories));
    }

    private static TransferService CreateTransferService(IPhotoLibraryService library, string statePath)
    {
        return new TransferService(
            library,
            new HashService(),
            new JsonTransferStateStore(statePath),
            NullLogger<TransferService>.Instance);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopAt = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < stopAt)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Condition was not reached before timeout.");
    }

    private sealed class SingleItemPhotoLibraryService : IPhotoLibraryService
    {
        private readonly MediaItem _item;
        private readonly byte[] _bytes;

        public SingleItemPhotoLibraryService(MediaItem item, byte[] bytes)
        {
            _item = item;
            _bytes = bytes;
        }

        public Task<IReadOnlyList<MediaItem>> ScanMediaAsync(DeviceInfo device, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MediaItem>>(new[] { _item });
        }

        public Task<Stream> OpenMediaStreamAsync(DeviceInfo device, MediaItem mediaItem, CancellationToken cancellationToken = default)
        {
            Stream stream = new MemoryStream(_bytes, writable: false);
            return Task.FromResult(stream);
        }
    }

    private sealed class SlowPhotoLibraryService : IPhotoLibraryService
    {
        private readonly MediaItem _item;

        public SlowPhotoLibraryService(MediaItem item)
        {
            _item = item;
        }

        public Task<IReadOnlyList<MediaItem>> ScanMediaAsync(DeviceInfo device, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MediaItem>>(new[] { _item });
        }

        public Task<Stream> OpenMediaStreamAsync(DeviceInfo device, MediaItem mediaItem, CancellationToken cancellationToken = default)
        {
            Stream stream = new SlowPatternStream(mediaItem.SizeBytes);
            return Task.FromResult(stream);
        }
    }

    private sealed class SlowPatternStream : Stream
    {
        private readonly long _length;
        private long _position;

        public SlowPatternStream(long length)
        {
            _length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= _length)
            {
                return 0;
            }

            await Task.Delay(20, cancellationToken);
            var toRead = (int)Math.Min(buffer.Length, _length - _position);
            for (var i = 0; i < toRead; i++)
            {
                buffer.Span[i] = (byte)((_position + i) % 251);
            }

            _position += toRead;
            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        private TempWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "iPhotoDropper-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempWorkspace(root);
        }

        public string CreateDirectory(string name)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Temp cleanup should not hide the test result.
            }
        }
    }
}
