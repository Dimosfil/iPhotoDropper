using System.Text.Json;
using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Models;
using iPhotoDropper.Infrastructure.Models;

namespace iPhotoDropper.Infrastructure.Services;

public sealed class JsonTransferStateStore : ITransferStateStore
{
    private readonly string _stateFilePath;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true
    };

    public JsonTransferStateStore(string? stateFilePath = null)
    {
        _stateFilePath = stateFilePath ?? GetDefaultStatePath();
    }

    public async Task<HashSet<string>> GetImportedKeysAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateInternalAsync(cancellationToken);
            if (state.Devices.TryGetValue(deviceId, out var value))
            {
                return new HashSet<string>(value.ImportedKeys, StringComparer.Ordinal);
            }
        }
        finally
        {
            _sync.Release();
        }

        return new HashSet<string>(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<TransferStateEntry>> GetHistoryAsync(string deviceId, int take = 100, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateInternalAsync(cancellationToken);
            if (!state.Devices.TryGetValue(deviceId, out var value))
            {
                return Array.Empty<TransferStateEntry>();
            }

            return value.Imports
                .TakeLast(Math.Max(1, take))
                .Select(x => new TransferStateEntry
                {
                    DeviceId = deviceId,
                    MediaKey = x.Key,
                    MediaId = x.Value.MediaId,
                    DestinationPath = x.Value.DestinationPath,
                    ImportedAtUtc = x.Value.ImportedAtUtc
                })
                .ToArray();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<bool> IsImportedAsync(string deviceId, string mediaKey, CancellationToken cancellationToken = default)
    {
        var keys = await GetImportedKeysAsync(deviceId, cancellationToken);
        return keys.Contains(mediaKey);
    }

    public async Task MarkImportedAsync(string deviceId, string mediaKey, string mediaId, string destinationPath, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateInternalAsync(cancellationToken);
            if (!state.Devices.TryGetValue(deviceId, out var value))
            {
                value = new PersistedDeviceState();
                state.Devices[deviceId] = value;
            }

            value.ImportedKeys.Add(mediaKey);
            value.Imports[mediaKey] = new PersistedImportRecord
            {
                MediaId = mediaId,
                DestinationPath = destinationPath,
                ImportedAtUtc = DateTimeOffset.UtcNow
            };

            await SaveStateInternalAsync(state, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task<PersistedTransferState> LoadStateInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_stateFilePath))
        {
            return new PersistedTransferState();
        }

        await using var stream = File.OpenRead(_stateFilePath);
        var loaded = await JsonSerializer.DeserializeAsync<PersistedTransferState>(stream, _json, cancellationToken);
        return loaded ?? new PersistedTransferState();
    }

    private async Task SaveStateInternalAsync(PersistedTransferState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_stateFilePath);
        await JsonSerializer.SerializeAsync(stream, state, _json, cancellationToken);
    }

    private static string GetDefaultStatePath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "iPhotoDropper", "state", "transfer-state.json");
    }
}
