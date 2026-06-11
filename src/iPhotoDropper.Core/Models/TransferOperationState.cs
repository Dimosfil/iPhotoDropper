namespace iPhotoDropper.Core.Models;

public enum TransferOperationState
{
    Idle,
    Running,
    Paused,
    Completed,
    Canceled,
    Failed
}
