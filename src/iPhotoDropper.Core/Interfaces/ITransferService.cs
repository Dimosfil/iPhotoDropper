using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Core.Interfaces;

public interface ITransferService
{
    TransferOperationState State { get; }
    bool IsPaused { get; }

    event EventHandler<TransferProgress>? ProgressChanged;
    event EventHandler<string>? LogChanged;

    Task<TransferResult> ImportAsync(
        DeviceInfo device,
        IReadOnlyList<MediaItem> mediaItems,
        TransferOptions options,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    void Pause();
    void Resume();
    void Cancel();
}
