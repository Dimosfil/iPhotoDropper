namespace iPhotoDropper.Core.Models;

public sealed class TransferOptions
{
    public string DestinationFolder { get; set; } = string.Empty;
    public bool CopyOnlyNew { get; set; } = true;
    public bool IncludePhotos { get; set; } = true;
    public bool IncludeVideos { get; set; } = true;
    public bool OrganizeByDateFolders { get; set; }
    public long? MaxFileSizeBytes { get; set; }
    public int RetryCount { get; set; } = 2;
    public int MaxParallelTransfers { get; set; } = 1;
    public int RetryBaseDelayMs { get; set; } = 1000;
    public ExistingFileAction DefaultExistingFileAction { get; set; } = ExistingFileAction.Skip;
    public Func<ExistingFileConflict, CancellationToken, Task<ExistingFileAction>>? ExistingFileConflictHandler { get; set; }
}

public enum ExistingFileAction
{
    Skip,
    Replace
}

public sealed class ExistingFileConflict
{
    public string SourceFileName { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public long SourceSizeBytes { get; init; }
    public long ExistingSizeBytes { get; init; }
    public DateTimeOffset ExistingModifiedAt { get; init; }
}
