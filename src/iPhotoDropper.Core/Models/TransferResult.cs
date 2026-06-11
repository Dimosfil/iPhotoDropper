using System.Collections.ObjectModel;

namespace iPhotoDropper.Core.Models;

public sealed class TransferResult
{
    public TransferOperationState State { get; set; } = TransferOperationState.Idle;
    public int FoundItems { get; set; }
    public int SelectedItems { get; set; }
    public int CopiedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public bool WasCanceled { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ObservableCollection<TransferResultItem> Items { get; set; } = new();
}
