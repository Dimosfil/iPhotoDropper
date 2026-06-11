namespace iPhotoDropper.Core.Models;

public sealed class TransferResultItem
{
    public string MediaId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public TransferErrorCode ErrorCode { get; set; } = TransferErrorCode.None;
    public string? ErrorMessage { get; set; }
    public string? DestinationPath { get; set; }
}
