using iPhotoDropper.Core.Models;

namespace iPhotoDropper.Core.Interfaces;

public interface ITransferStateStore
{
    Task<HashSet<string>> GetImportedKeysAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<bool> IsImportedAsync(string deviceId, string mediaKey, CancellationToken cancellationToken = default);
    Task MarkImportedAsync(string deviceId, string mediaKey, string mediaId, string destinationPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransferStateEntry>> GetHistoryAsync(string deviceId, int take = 100, CancellationToken cancellationToken = default);
}
