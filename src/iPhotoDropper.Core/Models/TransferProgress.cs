namespace iPhotoDropper.Core.Models;

public sealed class TransferProgress
{
    public TransferOperationState State { get; init; } = TransferOperationState.Idle;
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int CopiedItems { get; init; }
    public int SkippedItems { get; init; }
    public int FailedItems { get; init; }
    public long CurrentItemBytesTransferred { get; init; }
    public long CurrentItemBytesTotal { get; init; }
    public long TotalBytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public int CurrentItemRetry { get; init; }
    public string? CurrentItemName { get; init; }
    public string? CurrentItemOutputPath { get; init; }
    public string? Message { get; init; }

    public int OverallPercent
    {
        get
        {
            var bytesPercent = TotalBytes <= 0
                ? 0
                : (int)Math.Clamp(Math.Round((double)TotalBytesTransferred / TotalBytes * 100, 0), 0, 100);
            var itemsPercent = TotalItems <= 0
                ? 0
                : (int)Math.Clamp(Math.Round((double)ProcessedItems / TotalItems * 100, 0), 0, 100);

            return Math.Max(bytesPercent, itemsPercent);
        }
    }

    public int CurrentFilePercent =>
        CurrentItemBytesTotal <= 0
            ? 0
            : (int)Math.Clamp(Math.Round((double)CurrentItemBytesTransferred / CurrentItemBytesTotal * 100, 0), 0, 100);
}
