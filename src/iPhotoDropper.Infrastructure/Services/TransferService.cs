using System.Diagnostics;
using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace iPhotoDropper.Infrastructure.Services;

public sealed class TransferService : ITransferService
{
    private readonly IPhotoLibraryService _photoLibraryService;
    private readonly IHashService _hashService;
    private readonly ITransferStateStore _stateStore;
    private readonly ILogger<TransferService> _logger;

    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly ManualResetEventSlim _pauseSignal = new(true);
    private CancellationTokenSource? _activeRunCts;
    private TransferOperationState _state = TransferOperationState.Idle;

    public TransferService(
        IPhotoLibraryService photoLibraryService,
        IHashService hashService,
        ITransferStateStore stateStore,
        ILogger<TransferService> logger)
    {
        _photoLibraryService = photoLibraryService;
        _hashService = hashService;
        _stateStore = stateStore;
        _logger = logger;
    }

    public TransferOperationState State => _state;
    public bool IsPaused => _state == TransferOperationState.Paused;

    public event EventHandler<TransferProgress>? ProgressChanged;
    public event EventHandler<string>? LogChanged;

    public async Task<TransferResult> ImportAsync(
        DeviceInfo device,
        IReadOnlyList<MediaItem> mediaItems,
        TransferOptions options,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (device is null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (mediaItems is null || mediaItems.Count == 0)
        {
            return new TransferResult { State = TransferOperationState.Completed };
        }

        await _runLock.WaitAsync(cancellationToken);
        try
        {
            if (_state == TransferOperationState.Running)
            {
                throw new InvalidOperationException("Перед запуском проверьте, что текущий импорт завершен.");
            }

            _activeRunCts?.Dispose();
            _activeRunCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _activeRunCts.Token;

            _pauseSignal.Set();
            _state = TransferOperationState.Running;

            var startAt = DateTimeOffset.UtcNow;
            var result = new TransferResult
            {
                State = _state,
                StartedAtUtc = startAt,
                FoundItems = mediaItems.Count,
                FoundBytes = mediaItems.Sum(x => Math.Max(0, x.SizeBytes))
            };

            var filteredItems = mediaItems
                .Where(x => (x.Kind == MediaKind.Photo && options.IncludePhotos) || (x.Kind == MediaKind.Video && options.IncludeVideos))
                .Where(x => options.MaxFileSizeBytes is null || x.SizeBytes <= options.MaxFileSizeBytes.Value)
                .ToArray();

            var totalBytes = filteredItems.Sum(x => Math.Max(0, x.SizeBytes));
            result.SelectedItems = filteredItems.Length;
            result.SelectedBytes = totalBytes;
            EnsureDestinationHasFreeSpace(options.DestinationFolder, totalBytes);
            var copied = 0;
            var skipped = 0;
            var failed = 0;
            var processed = 0;
            var totalBytesTransferred = 0L;
            var copiedBytes = 0L;
            var skippedBytes = 0L;
            var failedBytes = 0L;

            LogChanged?.Invoke(this, $"Найдено {filteredItems.Length} файлов для импорта.");

            for (var i = 0; i < filteredItems.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                _pauseSignal.Wait(token);

                var media = filteredItems[i];
                processed++;
                var itemKey = _hashService.BuildMediaKey(device, media);
                var fallbackKey = _hashService.BuildFallbackMediaKey(device, media);

                var progressUpdate = new TransferProgress
                {
                    State = _state,
                    TotalItems = filteredItems.Length,
                    ProcessedItems = processed - 1,
                    CopiedItems = copied,
                    SkippedItems = skipped,
                    FailedItems = failed,
                    TotalBytes = totalBytes,
                    TotalBytesTransferred = totalBytesTransferred,
                    CurrentItemName = media.FileName
                };

                PublishProgress(progressUpdate, progress);

                if (_state == TransferOperationState.Paused)
                {
                    _pauseSignal.Wait(token);
                }

                if (options.CopyOnlyNew && ShouldSkipFromImportHistory(itemKey, fallbackKey))
                {
                    skipped++;
                    skippedBytes += Math.Max(0, media.SizeBytes);
                    result.Items.Add(new TransferResultItem
                    {
                        MediaId = media.Id,
                        FileName = media.FileName,
                        Success = false,
                        ErrorCode = TransferErrorCode.None,
                        ErrorMessage = "Пропущено (дубликат)"
                    });

                    PublishProgress(new TransferProgress
                    {
                        State = _state,
                        TotalItems = filteredItems.Length,
                        ProcessedItems = processed,
                        CopiedItems = copied,
                        SkippedItems = skipped,
                        FailedItems = failed,
                        TotalBytes = totalBytes,
                        TotalBytesTransferred = totalBytesTransferred,
                        CurrentItemName = media.FileName,
                        CurrentItemBytesTransferred = Math.Max(1, media.SizeBytes),
                        CurrentItemBytesTotal = Math.Max(1, media.SizeBytes),
                        Message = "Пропущено (дубликат)"
                    }, progress);
                    continue;
                }

                var destinationPath = BuildDestinationPath(media, options);
                if (File.Exists(destinationPath))
                {
                    var existingFileAction = await ResolveExistingFileActionAsync(media, destinationPath, options, token);
                    if (existingFileAction == ExistingFileAction.Skip)
                    {
                        skipped++;
                        skippedBytes += Math.Max(0, media.SizeBytes);
                        result.Items.Add(new TransferResultItem
                        {
                            MediaId = media.Id,
                            FileName = media.FileName,
                            Success = false,
                            ErrorCode = TransferErrorCode.None,
                            ErrorMessage = "Пропущено (файл уже существует)"
                        });

                        PublishProgress(new TransferProgress
                        {
                            State = _state,
                            TotalItems = filteredItems.Length,
                            ProcessedItems = processed,
                            CopiedItems = copied,
                            SkippedItems = skipped,
                            FailedItems = failed,
                            TotalBytes = totalBytes,
                            TotalBytesTransferred = totalBytesTransferred,
                            CurrentItemName = media.FileName,
                            CurrentItemBytesTransferred = Math.Max(1, media.SizeBytes),
                            CurrentItemBytesTotal = Math.Max(1, media.SizeBytes),
                            Message = "Пропущено (файл уже существует)"
                        }, progress);
                        continue;
                    }

                    LogChanged?.Invoke(this, $"Замена: {media.FileName}");
                }

                LogChanged?.Invoke(this, $"Импорт: {media.FileName}");

                var tempPath = destinationPath + ".tmp";
                var itemSw = Stopwatch.StartNew();
                var itemCompleted = false;
                try
                {
                    var transferProgress = new TransferProgress
                    {
                        State = progressUpdate.State,
                        TotalItems = progressUpdate.TotalItems,
                        ProcessedItems = progressUpdate.ProcessedItems,
                        CopiedItems = progressUpdate.CopiedItems,
                        SkippedItems = progressUpdate.SkippedItems,
                        FailedItems = progressUpdate.FailedItems,
                        TotalBytes = progressUpdate.TotalBytes,
                        TotalBytesTransferred = progressUpdate.TotalBytesTransferred,
                        CurrentItemBytesTransferred = progressUpdate.CurrentItemBytesTransferred,
                        CurrentItemBytesTotal = progressUpdate.CurrentItemBytesTotal,
                        CurrentItemRetry = progressUpdate.CurrentItemRetry,
                        CurrentItemName = media.FileName,
                        CurrentItemOutputPath = progressUpdate.CurrentItemOutputPath,
                        Message = progressUpdate.Message
                    };

                    var success = await CopyItemWithRetryAsync(device, media, destinationPath, tempPath, options, token, progress, transferProgress);
                    itemSw.Stop();

                    if (!success)
                    {
                        failed++;
                        failedBytes += Math.Max(0, media.SizeBytes);
                        result.Items.Add(new TransferResultItem
                        {
                            MediaId = media.Id,
                            FileName = media.FileName,
                            Success = false,
                            ErrorCode = TransferErrorCode.TransferInterrupted,
                            ErrorMessage = "Не удалось скопировать после всех попыток"
                        });
                        continue;
                    }

                    var finalPath = destinationPath;
                    File.Move(tempPath, finalPath, overwrite: File.Exists(finalPath));

                    await VerifyFileSizeAsync(media, finalPath, token);
                    ApplyOriginalFileTimestamps(media, finalPath);
                    await _stateStore.MarkImportedAsync(device.DeviceId, itemKey, media.Id, finalPath, token);
                    if (!string.Equals(itemKey, fallbackKey, StringComparison.Ordinal))
                    {
                        await _stateStore.MarkImportedAsync(device.DeviceId, fallbackKey, media.Id, finalPath, token);
                    }
                    copied++;
                    copiedBytes += Math.Max(0, new FileInfo(finalPath).Length);
                    result.Items.Add(new TransferResultItem
                    {
                        MediaId = media.Id,
                        FileName = media.FileName,
                        Success = true,
                        DestinationPath = finalPath
                    });
                    itemCompleted = true;

                    totalBytesTransferred += Math.Max(0, media.SizeBytes);
                    LogChanged?.Invoke(this, $"OK: {media.FileName} за {itemSw.Elapsed}");
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        result.WasCanceled = true;
                        _state = TransferOperationState.Canceled;
                        break;
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    failedBytes += Math.Max(0, media.SizeBytes);
                    result.Items.Add(new TransferResultItem
                    {
                        MediaId = media.Id,
                        FileName = media.FileName,
                        Success = false,
                        ErrorCode = ex is UnauthorizedAccessException ? TransferErrorCode.PermissionDenied : TransferErrorCode.TransferInterrupted,
                        ErrorMessage = ex.Message
                    });
                    _logger.LogError(ex, "Ошибка импорта файла {FileName}", media.FileName);
                    LogChanged?.Invoke(this, $"ERR: {media.FileName} — {ex.Message}");
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        TryDelete(tempPath);
                    }

                    result.CopiedCount = copied;
                    result.SkippedCount = skipped;
                    result.FailedCount = failed;
                    result.CopiedBytes = copiedBytes;
                    result.SkippedBytes = skippedBytes;
                    result.FailedBytes = failedBytes;
                    PublishProgress(new TransferProgress
                    {
                        State = _state,
                        TotalItems = filteredItems.Length,
                        ProcessedItems = processed,
                        CopiedItems = copied,
                        SkippedItems = skipped,
                        FailedItems = failed,
                        TotalBytes = totalBytes,
                        TotalBytesTransferred = Math.Min(totalBytesTransferred, totalBytes),
                        CurrentItemName = media.FileName,
                        CurrentItemBytesTransferred = itemCompleted ? Math.Max(1, media.SizeBytes) : 0,
                        CurrentItemBytesTotal = Math.Max(1, media.SizeBytes),
                        Message = _state == TransferOperationState.Canceled
                            ? "Импорт прерван"
                            : itemCompleted ? "Файл завершен" : "Файл не завершен"
                    }, progress);
                }
            }

            if (_state != TransferOperationState.Canceled)
            {
                _state = TransferOperationState.Completed;
            }

            result.State = _state;
            result.CopiedCount = copied;
            result.SkippedCount = skipped;
            result.FailedCount = failed;
            result.CopiedBytes = copiedBytes;
            result.SkippedBytes = skippedBytes;
            result.FailedBytes = failedBytes;
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            result.Duration = result.CompletedAtUtc - result.StartedAtUtc;
            return result;
        }
        finally
        {
            _state = _state == TransferOperationState.Canceled ? TransferOperationState.Canceled : TransferOperationState.Idle;
            _pauseSignal.Set();
            _runLock.Release();
            _activeRunCts?.Dispose();
            _activeRunCts = null;
        }
    }

    public void Pause()
    {
        if (_state != TransferOperationState.Running)
        {
            return;
        }

        _state = TransferOperationState.Paused;
        _pauseSignal.Reset();
        LogChanged?.Invoke(this, "Импорт приостановлен.");
        PublishProgress(new TransferProgress { State = _state });
    }

    public void Resume()
    {
        if (_state != TransferOperationState.Paused)
        {
            return;
        }

        _state = TransferOperationState.Running;
        _pauseSignal.Set();
        LogChanged?.Invoke(this, "Импорт возобновлён.");
        PublishProgress(new TransferProgress { State = _state });
    }

    public void Cancel()
    {
        if (_state is not (TransferOperationState.Running or TransferOperationState.Paused))
        {
            return;
        }

        _state = TransferOperationState.Canceled;
        _activeRunCts?.Cancel();
        _pauseSignal.Set();
        LogChanged?.Invoke(this, "Импорт отменен пользователем.");
        PublishProgress(new TransferProgress { State = _state });
    }

    private async Task<bool> CopyItemWithRetryAsync(
        DeviceInfo device,
        MediaItem item,
        string destinationPath,
        string tempPath,
        TransferOptions options,
        CancellationToken token,
        IProgress<TransferProgress>? progress,
        TransferProgress baseProgress)
    {
        var maxAttempts = Math.Max(0, options.RetryCount);
        var attempt = 0;
        Exception? lastError = null;

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        EnsureFreeSpace(destinationPath, item.SizeBytes);

        for (; attempt <= maxAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await using var source = await _photoLibraryService.OpenMediaStreamAsync(device, item, token);
                await CopyWithProgressAsync(source, tempPath, item, baseProgress, attempt + 1, options, progress, token);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                var delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMs * Math.Pow(2, attempt + 1));
                LogChanged?.Invoke(this, $"Повтор {attempt + 1}/{maxAttempts} для {item.FileName}: {ex.Message}");
                await Task.Delay(delay, token);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        if (lastError is TimeoutException)
        {
            throw lastError;
        }

        _logger.LogWarning(lastError, "Неуспешный импорт после {Attempts} попыток: {FileName}", maxAttempts + 1, item.FileName);
        return false;
    }

    private async Task CopyWithProgressAsync(
        Stream source,
        string tempPath,
        MediaItem item,
        TransferProgress baseProgress,
        int attempt,
        TransferOptions options,
        IProgress<TransferProgress>? progress,
        CancellationToken token)
    {
        const int bufferSize = 128 * 1024;
        await using var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous);
        var buffer = new byte[bufferSize];
        var transferred = 0L;
        var currentItemBytesTotal = ResolveCurrentItemBytesTotal(source, item);
        var progressClock = Stopwatch.StartNew();
        var lastPublishedPercent = -1;

        while (true)
        {
            token.ThrowIfCancellationRequested();
            _pauseSignal.Wait(token);

            var read = await ReadWithNoProgressTimeoutAsync(
                source,
                buffer.AsMemory(0, buffer.Length),
                item,
                ResolveNoProgressTimeout(options),
                token);
            if (read <= 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            transferred += read;
            var currentPercent = currentItemBytesTotal <= 0
                ? 0
                : (int)Math.Clamp(Math.Round((double)transferred / currentItemBytesTotal * 100, 0), 0, 100);
            var isFinalChunk = item.SizeBytes > 0 && transferred >= currentItemBytesTotal;
            if (!isFinalChunk && currentPercent == lastPublishedPercent && progressClock.ElapsedMilliseconds < 200)
            {
                continue;
            }

            lastPublishedPercent = currentPercent;
            progressClock.Restart();
            PublishProgress(new TransferProgress
            {
                State = _state,
                TotalItems = baseProgress.TotalItems,
                ProcessedItems = baseProgress.ProcessedItems,
                CopiedItems = baseProgress.CopiedItems,
                SkippedItems = baseProgress.SkippedItems,
                FailedItems = baseProgress.FailedItems,
                TotalBytes = baseProgress.TotalBytes,
                TotalBytesTransferred = baseProgress.TotalBytesTransferred + transferred,
                CurrentItemBytesTransferred = transferred,
                CurrentItemBytesTotal = currentItemBytesTotal,
                CurrentItemName = item.FileName,
                CurrentItemRetry = attempt,
                CurrentItemOutputPath = baseProgress.CurrentItemOutputPath,
                Message = $"Скопировано {FormatBytes(transferred)}"
            }, progress);
        }

        await destination.FlushAsync(token);
    }

    private static TimeSpan ResolveNoProgressTimeout(TransferOptions options)
    {
        var timeoutMs = options.NoProgressTimeoutMs <= 0
            ? 60_000
            : Math.Max(100, options.NoProgressTimeoutMs);

        return TimeSpan.FromMilliseconds(timeoutMs);
    }

    private static async ValueTask<int> ReadWithNoProgressTimeoutAsync(
        Stream source,
        Memory<byte> buffer,
        MediaItem item,
        TimeSpan timeout,
        CancellationToken token)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var readTask = source.ReadAsync(buffer, token).AsTask();
        var timeoutTask = Task.Delay(timeout, timeoutCts.Token);
        var completedTask = await Task.WhenAny(readTask, timeoutTask);
        if (completedTask == readTask)
        {
            timeoutCts.Cancel();
            return await readTask;
        }

        token.ThrowIfCancellationRequested();

        try
        {
            source.Dispose();
        }
        catch
        {
            // Best-effort abort for native MTP streams that stopped producing data.
        }

        _ = readTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        throw new TimeoutException($"No read progress for {item.FileName} during {timeout.TotalSeconds:0} seconds.");
    }

    private static long ResolveCurrentItemBytesTotal(Stream source, MediaItem item)
    {
        if (item.SizeBytes > 0)
        {
            return item.SizeBytes;
        }

        if (source.CanSeek)
        {
            try
            {
                return Math.Max(1, source.Length);
            }
            catch
            {
                return 1;
            }
        }

        return 1;
    }

    private string BuildDestinationPath(MediaItem item, TransferOptions options)
    {
        var baseFolder = options.DestinationFolder;
        var fileName = SanitizeFileName(Path.GetFileNameWithoutExtension(item.FileName));
        var ext = Path.GetExtension(item.FileName);
        var suffix = ComputeShortHash(item.Id);
        var finalFileName = $"{fileName}_{suffix}{ext}";

        var folder = baseFolder;
        if (options.OrganizeByDateFolders && item.CapturedAt.HasValue)
        {
            var dateFolder = item.CapturedAt.Value.ToLocalTime().ToString("yyyy\\MM");
            folder = Path.Combine(folder, dateFolder);
        }

        Directory.CreateDirectory(folder);
        return Path.Combine(folder, finalFileName);
    }

    private static async Task<ExistingFileAction> ResolveExistingFileActionAsync(
        MediaItem item,
        string destinationPath,
        TransferOptions options,
        CancellationToken token)
    {
        var handler = options.ExistingFileConflictHandler;
        if (handler is null)
        {
            return options.DefaultExistingFileAction;
        }

        var existing = new FileInfo(destinationPath);
        var conflict = new ExistingFileConflict
        {
            SourceFileName = item.FileName,
            DestinationPath = destinationPath,
            SourceSizeBytes = item.SizeBytes,
            ExistingSizeBytes = existing.Exists ? existing.Length : 0,
            ExistingModifiedAt = existing.Exists ? existing.LastWriteTime : DateTimeOffset.MinValue
        };

        return await handler(conflict, token);
    }

    private async Task<HashSet<string>> GetImportedKeysForDestinationAsync(
        string deviceId,
        string destinationFolder,
        CancellationToken token)
    {
        var destinationRoot = NormalizeDirectoryPath(destinationFolder);
        var history = await _stateStore.GetHistoryAsync(deviceId, int.MaxValue, token);
        return history
            .Where(entry => IsPathUnderDirectory(entry.DestinationPath, destinationRoot))
            .Select(entry => entry.MediaKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsPathUnderDirectory(string path, string directoryRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(directoryRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeDirectoryPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static bool ShouldSkipFromImportHistory(string itemKey, string fallbackKey)
    {
        _ = itemKey;
        _ = fallbackKey;
        return false;
    }

    private static void EnsureFreeSpace(string destinationFilePath, long neededBytes)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destinationFilePath))!);
        if (!drive.IsReady)
        {
            return;
        }

        if (drive.AvailableFreeSpace < neededBytes + 8 * 1024 * 1024)
        {
            throw new IOException($"Недостаточно места на диске. Нужен дополнительный резерв: {FormatBytes(neededBytes)}");
        }
    }

    private static void EnsureDestinationHasFreeSpace(string destinationFolder, long neededBytes)
    {
        if (neededBytes <= 0)
        {
            return;
        }

        Directory.CreateDirectory(destinationFolder);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destinationFolder))!);
        if (!drive.IsReady)
        {
            return;
        }

        const long reserveBytes = 512L * 1024 * 1024;
        var requiredBytes = neededBytes > long.MaxValue - reserveBytes
            ? long.MaxValue
            : neededBytes + reserveBytes;
        if (drive.AvailableFreeSpace >= requiredBytes)
        {
            return;
        }

        throw new IOException(
            $"Недостаточно места на диске для импорта. Нужно {FormatBytes(neededBytes)} плюс резерв {FormatBytes(reserveBytes)}, доступно {FormatBytes(drive.AvailableFreeSpace)}.");
    }

    private static string ResolveFreePath(string folder, string fileName)
    {
        Directory.CreateDirectory(folder);
        var current = Path.Combine(folder, fileName);
        if (!File.Exists(current))
        {
            return current;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 1; i < 1000; i++)
        {
            var next = Path.Combine(folder, $"{name} ({i}){ext}");
            if (!File.Exists(next))
            {
                return next;
            }
        }

        return current;
    }

    private static async Task VerifyFileSizeAsync(MediaItem item, string destinationPath, CancellationToken token)
    {
        await Task.Run(() =>
        {
            var fi = new FileInfo(destinationPath);
            if (!fi.Exists)
            {
                throw new FileNotFoundException("Файл не создан после копирования", destinationPath);
            }

            if (fi.Length != item.SizeBytes)
            {
                throw new IOException($"Нарушена целостность файла: ожидаемо {item.SizeBytes} байт, получено {fi.Length}.");
            }
        }, token);
    }

    private void ApplyOriginalFileTimestamps(MediaItem item, string destinationPath)
    {
        var modifiedAt = item.SourceModifiedAt ?? item.CapturedAt;
        var createdAt = item.SourceCreatedAt ?? modifiedAt;

        if (createdAt is null && modifiedAt is null)
        {
            return;
        }

        try
        {
            if (createdAt.HasValue)
            {
                File.SetCreationTimeUtc(destinationPath, createdAt.Value.UtcDateTime);
            }

            if (modifiedAt.HasValue)
            {
                File.SetLastWriteTimeUtc(destinationPath, modifiedAt.Value.UtcDateTime);
                File.SetLastAccessTimeUtc(destinationPath, modifiedAt.Value.UtcDateTime);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Could not preserve timestamps for imported file {FileName}", item.FileName);
        }
    }

    private void PublishProgress(TransferProgress progress, IProgress<TransferProgress>? externalProgress = null)
    {
        ProgressChanged?.Invoke(this, progress);
        externalProgress?.Report(progress);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors to keep main flow stable.
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash.AsSpan(0, 3));
    }

    private static string FormatBytes(long bytes)
    {
        const int step = 1024;
        if (bytes < step)
        {
            return $"{bytes} B";
        }

        if (bytes <= 0)
        {
            return $"{bytes} B";
        }

        var exp = (int)(Math.Log(bytes, step));
        var suffix = "KMGTPE"[(exp - 1)..exp] + "B";
        var value = bytes / Math.Pow(step, exp);
        return $"{value:0.00} {suffix}";
    }
}
