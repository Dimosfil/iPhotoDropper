using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using iPhotoDropper.Core.Interfaces;
using iPhotoDropper.Core.Events;
using iPhotoDropper.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace iPhotoDropper.App.ViewModels;

public sealed class TransferViewModel : INotifyPropertyChanged
{
    private readonly IUsbDeviceService _usbService;
    private readonly IPhotoLibraryService _libraryService;
    private readonly ITransferService _transferService;
    private readonly ILogger<TransferViewModel> _logger;
    private readonly DispatcherQueue _dispatcher;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _importCts;
    private int _progressRunVersion;

    private DeviceInfo? _selectedDevice;
    private string _deviceStatus = "Ожидание устройства";
    private string _destinationFolder;
    private bool _copyOnlyNew = true;
    private bool _copyPhotos = true;
    private bool _copyVideos = true;
    private bool _organizeByDate;
    private string? _maxFileSizeMbText;
    private bool _isBusy;
    private bool _isScanning;
    private bool _isPaused;
    private string _progressText = "Готов.";
    private int _overallProgress;
    private int _currentFileProgress;
    private string _lastReport = "нет отчёта";
    private string _importSummary = "Импорт ещё не запускался.";
    private string _currentFileName = string.Empty;

    public TransferViewModel(
        IUsbDeviceService usbService,
        IPhotoLibraryService libraryService,
        ITransferService transferService,
        ILogger<TransferViewModel> logger)
    {
        _usbService = usbService;
        _libraryService = libraryService;
        _transferService = transferService;
        _logger = logger;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _destinationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "iPhotoDropper");

        Devices = new ObservableCollection<DeviceInfo>();
        MediaItems = new ObservableCollection<TransferItemViewModel>();
        LogLines = new ObservableCollection<string>();

        ScanCommand = new AsyncRelayCommand(ScanAsync, CanScan);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        PauseCommand = new RelayCommand(PauseTransfer, CanPause);
        ResumeCommand = new RelayCommand(ResumeTransfer, CanResume);
        CancelCommand = new RelayCommand(CancelTransfer, CanCancel);

        _usbService.DeviceConnected += OnDeviceConnectionChanged;
        _usbService.DeviceDisconnected += OnDeviceConnectionChanged;
        _transferService.LogChanged += OnTransferLog;

        _ = RefreshDevicesAsync();
    }

    public ICommand ScanCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand CancelCommand { get; }

    public ObservableCollection<DeviceInfo> Devices { get; }
    public ObservableCollection<TransferItemViewModel> MediaItems { get; }
    public ObservableCollection<string> LogLines { get; }
    public Func<ExistingFileConflict, CancellationToken, Task<ExistingFileAction>>? ExistingFileConflictResolver { get; set; }

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (IsSameDeviceSession(_selectedDevice, value))
            {
                return;
            }
            _selectedDevice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDeviceReady));
            if (!IsBusy)
            {
                ResetDeviceSessionState(value is null
                    ? "Устройство отключено."
                    : $"Готово к сканированию: {value.DisplayName}");
            }
            UpdateStatusFromDevice();
            UpdateCommands();
        }
    }

    public string DeviceStatus
    {
        get => _deviceStatus;
        private set
        {
            if (_deviceStatus == value)
            {
                return;
            }
            _deviceStatus = value;
            OnPropertyChanged();
        }
    }

    public string DestinationFolder
    {
        get => _destinationFolder;
        set
        {
            if (_destinationFolder == value)
            {
                return;
            }
            _destinationFolder = value;
            OnPropertyChanged();
            UpdateCommands();
        }
    }

    public bool CopyOnlyNew
    {
        get => _copyOnlyNew;
        set
        {
            _copyOnlyNew = value;
            OnPropertyChanged();
        }
    }

    public bool CopyPhotos
    {
        get => _copyPhotos;
        set
        {
            _copyPhotos = value;
            OnPropertyChanged();
        }
    }

    public bool CopyVideos
    {
        get => _copyVideos;
        set
        {
            _copyVideos = value;
            OnPropertyChanged();
        }
    }

    public bool OrganizeByDate
    {
        get => _organizeByDate;
        set
        {
            _organizeByDate = value;
            OnPropertyChanged();
        }
    }

    public string? MaxFileSizeMbText
    {
        get => _maxFileSizeMbText;
        set
        {
            _maxFileSizeMbText = value;
            OnPropertyChanged();
        }
    }

    public bool IsDeviceReady => SelectedDevice is { IsConnected: true };
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }
            _isBusy = value;
            OnPropertyChanged();
            UpdateCommands();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (_isScanning == value)
            {
                return;
            }
            _isScanning = value;
            OnPropertyChanged();
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            _isPaused = value;
            OnPropertyChanged();
            UpdateCommands();
        }
    }

    public string ProgressText
    {
        get => _progressText;
        private set
        {
            if (_progressText == value)
            {
                return;
            }
            _progressText = value;
            OnPropertyChanged();
        }
    }

    public int OverallProgress
    {
        get => _overallProgress;
        private set
        {
            if (_overallProgress == value)
            {
                return;
            }
            _overallProgress = value;
            OnPropertyChanged();
        }
    }

    public int CurrentFileProgress
    {
        get => _currentFileProgress;
        private set
        {
            if (_currentFileProgress == value)
            {
                return;
            }
            _currentFileProgress = value;
            OnPropertyChanged();
        }
    }

    public string LastReport
    {
        get => _lastReport;
        private set
        {
            if (_lastReport == value)
            {
                return;
            }
            _lastReport = value;
            OnPropertyChanged();
        }
    }

    public string ImportSummary
    {
        get => _importSummary;
        private set
        {
            if (_importSummary == value)
            {
                return;
            }
            _importSummary = value;
            OnPropertyChanged();
        }
    }

    public string CurrentFileName
    {
        get => _currentFileName;
        private set
        {
            if (_currentFileName == value)
            {
                return;
            }
            _currentFileName = value;
            OnPropertyChanged();
        }
    }

    public int SelectedCount => MediaItems.Count(x => x.IsSelected);

    public long SelectedBytes => MediaItems.Where(x => x.IsSelected).Sum(x => Math.Max(0, x.Source.SizeBytes));

    public string SelectionSummary => $"Выбрано: {SelectedCount} / {FormatBytes(SelectedBytes)}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task ScanAsync()
    {
        var selectedDevice = SelectedDevice;
        if (IsBusy || selectedDevice is null || !selectedDevice.IsConnected)
        {
            return;
        }

        IsBusy = true;
        IsScanning = true;
        ResetDeviceSessionState($"Сканируем: {selectedDevice.DisplayName}");
        AddLog("scan: старт");
        AddLog($"scan: устройство = {selectedDevice.DisplayName}, transport = {selectedDevice.Transport ?? "USB"}, trusted = {selectedDevice.IsTrusted}");
        AddLog($"scan: источник = {(selectedDevice.DeviceId.StartsWith("mtp:", StringComparison.Ordinal) ? "real iPhone MTP/DCIM" : "mock folder source")}");
        AddLog($"scan: фильтры = photos:{CopyPhotos}, videos:{CopyVideos}, maxMB:{MaxFileSizeMbText ?? "none"}");
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var scanCts = _scanCts;
        var includePhotos = CopyPhotos;
        var includeVideos = CopyVideos;
        long? maxBytes = null;
        if (long.TryParse(MaxFileSizeMbText, out var maxMb) && maxMb > 0)
        {
            maxBytes = maxMb * 1024 * 1024;
        }

        var liveItemsEnabled = 1;
        var filteredCount = 0;
        long filteredBytes = 0;
        var itemProgress = new SynchronousProgress<MediaItem>(item =>
        {
            if (!MatchesScanFilters(item, includePhotos, includeVideos, maxBytes))
            {
                return;
            }

            var count = Interlocked.Increment(ref filteredCount);
            var bytes = Interlocked.Add(ref filteredBytes, Math.Max(0, item.SizeBytes));
            _dispatcher.TryEnqueue(() =>
            {
                if (Volatile.Read(ref liveItemsEnabled) == 0 || _scanCts != scanCts || scanCts.IsCancellationRequested)
                {
                    return;
                }

                AddScannedMediaItem(item, count, bytes, count <= 40);
            });
        });

        try
        {
            var scanned = await Task.Run(
                () => _libraryService.ScanMediaAsync(selectedDevice, scanCts.Token, itemProgress),
                scanCts.Token);
            Volatile.Write(ref liveItemsEnabled, 0);
            AddLog($"scan: получено от источника = {scanned.Count}");
            var filteredItems = scanned
                .Where(x => MatchesScanFilters(x, includePhotos, includeVideos, maxBytes))
                .ToArray();
            ReplaceMediaItems(filteredItems);

            var finalFilteredBytes = filteredItems.Sum(x => Math.Max(0, x.SizeBytes));
            ProgressText = $"Найдено элементов: {filteredItems.Length}";
            ImportSummary = $"Найдено: {filteredItems.Length} файлов / {FormatBytes(finalFilteredBytes)}. Выбрано: {filteredItems.Length} / {FormatBytes(finalFilteredBytes)}.";
            AddLog($"scan: после фильтров = {filteredItems.Length}");
        }
        catch (OperationCanceledException)
        {
            Volatile.Write(ref liveItemsEnabled, 0);
            AddLog("scan: отменено");
        }
        catch (Exception ex)
        {
            Volatile.Write(ref liveItemsEnabled, 0);
            AddLog($"scan:error {ex.Message}");
            _logger.LogError(ex, "Scan failed");
        }
        finally
        {
            IsScanning = false;
            IsBusy = false;
        }
    }

    private void AddScannedMediaItem(MediaItem item, int count, long totalBytes, bool addSampleLog)
    {
        var itemVm = new TransferItemViewModel { Source = item, IsSelected = true };
        itemVm.PropertyChanged += OnItemSelectionChanged;
        MediaItems.Add(itemVm);

        CurrentFileName = item.FileName;
        ProgressText = $"Найдено элементов: {count}";
        ImportSummary = $"Найдено: {count} файлов / {FormatBytes(totalBytes)}. Выбрано: {count} / {FormatBytes(totalBytes)}.";
        if (addSampleLog)
        {
            AddLog($"scan:file {item.FileName} | {item.Kind} | {FormatBytes(item.SizeBytes)} | {item.RelativePath ?? item.SourcePath ?? "no path"}");
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectionSummary));
        UpdateCommands();
    }

    private void ReplaceMediaItems(IReadOnlyList<MediaItem> items)
    {
        foreach (var item in MediaItems)
        {
            item.PropertyChanged -= OnItemSelectionChanged;
        }

        MediaItems.Clear();
        foreach (var item in items)
        {
            var itemVm = new TransferItemViewModel { Source = item, IsSelected = true };
            itemVm.PropertyChanged += OnItemSelectionChanged;
            MediaItems.Add(itemVm);
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectionSummary));
        UpdateCommands();
    }

    private static bool MatchesScanFilters(MediaItem item, bool includePhotos, bool includeVideos, long? maxBytes)
    {
        if (item.Kind == MediaKind.Photo && !includePhotos)
        {
            return false;
        }

        if (item.Kind == MediaKind.Video && !includeVideos)
        {
            return false;
        }

        return maxBytes is null || item.SizeBytes <= maxBytes.Value;
    }

    public async Task ImportAsync()
    {
        var selectedDevice = SelectedDevice;
        if (IsBusy || selectedDevice is null || MediaItems.Count == 0)
        {
            return;
        }

        IsBusy = true;
        IsPaused = false;
        OverallProgress = 0;
        CurrentFileProgress = 0;
        CurrentFileName = string.Empty;
        ProgressText = "Подготовка импорта";
        LastReport = "Импорт выполняется...";
        var selectedItems = MediaItems.Where(x => x.IsSelected).Select(x => x.Source).ToArray();
        var selectedBytes = selectedItems.Sum(x => Math.Max(0, x.SizeBytes));
        ImportSummary = $"К импорту: {selectedItems.Length} файлов / {FormatBytes(selectedBytes)}. Скопировано: 0 / 0 B.";
        AddLog($"import: старт, файлов = {selectedItems.Length}, объём = {FormatBytes(selectedBytes)}, папка = {DestinationFolder}");

        _importCts?.Cancel();
        _importCts = new CancellationTokenSource();
        var progressRunVersion = Interlocked.Increment(ref _progressRunVersion);

        try
        {
            var options = new TransferOptions
            {
                DestinationFolder = DestinationFolder,
                CopyOnlyNew = CopyOnlyNew,
                IncludePhotos = CopyPhotos,
                IncludeVideos = CopyVideos,
                OrganizeByDateFolders = OrganizeByDate,
                MaxFileSizeBytes = long.TryParse(MaxFileSizeMbText, out var maxMb) && maxMb > 0
                    ? maxMb * 1024 * 1024
                    : null,
                RetryCount = 2,
                RetryBaseDelayMs = 500,
                DefaultExistingFileAction = ExistingFileAction.Skip,
                ExistingFileConflictHandler = ExistingFileConflictResolver
            };

            if (selectedItems.Length == 0)
            {
                ProgressText = "Нет выбранных файлов";
                ImportSummary = "Нет выбранных файлов.";
                return;
            }

            var report = await Task.Run(
                () => _transferService.ImportAsync(
                    selectedDevice,
                    selectedItems,
                    options,
                    new Progress<TransferProgress>(progress => ApplyProgress(progress, progressRunVersion)),
                    _importCts.Token),
                _importCts.Token);

            Interlocked.Increment(ref _progressRunVersion);
            LastReport =
                $"Найдено {report.FoundItems} / {FormatBytes(report.FoundBytes)}; " +
                $"выбрано {report.SelectedItems} / {FormatBytes(report.SelectedBytes)}; " +
                $"скопировано {report.CopiedCount} / {FormatBytes(report.CopiedBytes)}; " +
                $"пропущено {report.SkippedCount} / {FormatBytes(report.SkippedBytes)}; " +
                $"ошибок {report.FailedCount} / {FormatBytes(report.FailedBytes)}; " +
                $"время {report.Duration:g}";
            ImportSummary = BuildCompletedImportSummary(report);
            if (report.State == TransferOperationState.Completed)
            {
                OverallProgress = 100;
                CurrentFileProgress = 100;
                ProgressText = "Импорт завершен";
            }

            AddLog($"import: завершено: {LastReport}");
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref _progressRunVersion);
            AddLog("import: отменен");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _progressRunVersion);
            AddLog($"import:error {ex.Message}");
            _logger.LogError(ex, "Import failed");
        }
        finally
        {
            IsBusy = false;
            IsPaused = false;
        }
    }

    private void OnItemSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransferItemViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedBytes));
            OnPropertyChanged(nameof(SelectionSummary));
            UpdateCommands();
        }
    }

    public void PauseTransfer()
    {
        _transferService.Pause();
        IsPaused = true;
        ProgressText = "Приостановлено";
    }

    public void ResumeTransfer()
    {
        _transferService.Resume();
        IsPaused = false;
        ProgressText = "Работа продолжается";
    }

    public void CancelTransfer()
    {
        _transferService.Cancel();
        _importCts?.Cancel();
        IsPaused = false;
        ProgressText = "Отменено пользователем";
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            var connected = await _usbService.GetConnectedDevicesAsync();
            _dispatcher.TryEnqueue(() =>
            {
                Devices.Clear();
                foreach (var device in connected)
                {
                    Devices.Add(device);
                }
                SelectedDevice = Devices.FirstOrDefault();
                UpdateStatusFromDevice();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refresh devices failed");
            AddLog($"device:error {ex.Message}");
        }
    }

    private void OnDeviceConnectionChanged(object? sender, DeviceConnectionEventArgs e)
    {
        _ = _dispatcher.TryEnqueue(() =>
        {
            if (e.DeviceInfo.IsConnected)
            {
                if (!Devices.Any(x => x.DeviceId == e.DeviceInfo.DeviceId))
                {
                    Devices.Add(e.DeviceInfo);
                }
                SelectedDevice = Devices.FirstOrDefault(x => x.DeviceId == e.DeviceInfo.DeviceId) ?? SelectedDevice;
                AddLog($"device: подключено {e.DeviceInfo.DisplayName}");
            }
            else
            {
                var existing = Devices.FirstOrDefault(x => x.DeviceId == e.DeviceInfo.DeviceId);
                if (existing is not null)
                {
                    Devices.Remove(existing);
                }
                if (SelectedDevice?.DeviceId == e.DeviceInfo.DeviceId)
                {
                    SelectedDevice = Devices.FirstOrDefault();
                }
                AddLog($"device: отключено {e.DeviceInfo.DisplayName}");
            }
            return;
        });
    }

    private void OnTransferProgressChanged(object? sender, TransferProgress progress)
    {
        ApplyProgress(progress, _progressRunVersion);
    }

    private void OnTransferLog(object? sender, string message)
    {
        _dispatcher.TryEnqueue(() =>
        {
            AddLog(message);
            while (LogLines.Count > 200)
            {
                LogLines.RemoveAt(LogLines.Count - 1);
            }
        });
    }

    private void ApplyProgress(TransferProgress progress, int progressRunVersion)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (progressRunVersion != _progressRunVersion)
            {
                return;
            }

            OverallProgress = progress.OverallPercent;
            CurrentFileProgress = progress.CurrentFilePercent;
            CurrentFileName = progress.CurrentItemName ?? string.Empty;

            DeviceStatus = progress.State switch
            {
                TransferOperationState.Running => "Передача",
                TransferOperationState.Paused => "Пауза",
                TransferOperationState.Completed => "Завершено",
                TransferOperationState.Canceled => "Отменено",
                TransferOperationState.Failed => "Ошибка",
                _ => "Готов"
            };

            if (!string.IsNullOrWhiteSpace(progress.Message))
            {
                ProgressText = progress.Message;
            }
            else
            {
                ProgressText = $"{progress.ProcessedItems}/{progress.TotalItems}";
            }

            ImportSummary = BuildProgressSummary(progress);
        });
    }

    private static string BuildProgressSummary(TransferProgress progress)
    {
        if (progress.TotalItems <= 0 && progress.TotalBytes <= 0)
        {
            return "Импорт ожидает данных.";
        }

        return
            $"Обработано: {progress.ProcessedItems}/{progress.TotalItems}; " +
            $"передано: {FormatBytes(Math.Min(progress.TotalBytesTransferred, progress.TotalBytes))} из {FormatBytes(progress.TotalBytes)}; " +
            $"скопировано: {progress.CopiedItems}; " +
            $"пропущено: {progress.SkippedItems}; " +
            $"ошибок: {progress.FailedItems}.";
    }

    private static string BuildCompletedImportSummary(TransferResult report)
    {
        return
            $"Перекинули: {report.CopiedCount} файлов / {FormatBytes(report.CopiedBytes)}. " +
            $"Пропущено: {report.SkippedCount} / {FormatBytes(report.SkippedBytes)}. " +
            $"Ошибок: {report.FailedCount} / {FormatBytes(report.FailedBytes)}.";
    }

    private void UpdateStatusFromDevice()
    {
        if (SelectedDevice is null)
        {
            DeviceStatus = "Нет устройства";
            return;
        }

        DeviceStatus = $"{SelectedDevice.DisplayName} — {SelectedDevice.StatusText}";
        UpdateCommands();
    }

    private bool CanScan()
    {
        return !IsBusy && IsDeviceReady;
    }

    private bool CanImport()
    {
        return !IsBusy && IsDeviceReady && MediaItems.Any(x => x.IsSelected) && !string.IsNullOrWhiteSpace(DestinationFolder);
    }

    private bool CanPause()
    {
        return IsBusy && !IsPaused;
    }

    private bool CanResume()
    {
        return IsBusy && IsPaused;
    }

    private bool CanCancel()
    {
        return IsBusy;
    }

    private void UpdateCommands()
    {
        if (ScanCommand is AsyncRelayCommand scan)
        {
            scan.RaiseCanExecuteChanged();
        }
        if (ImportCommand is AsyncRelayCommand import)
        {
            import.RaiseCanExecuteChanged();
        }
        if (PauseCommand is RelayCommand pause)
        {
            pause.NotifyCanExecuteChanged();
        }
        if (ResumeCommand is RelayCommand resume)
        {
            resume.NotifyCanExecuteChanged();
        }
        if (CancelCommand is RelayCommand cancel)
        {
            cancel.NotifyCanExecuteChanged();
        }
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanImport));
    }

    private void ResetDeviceSessionState(string progressText)
    {
        Interlocked.Increment(ref _progressRunVersion);
        _scanCts?.Cancel();
        _importCts?.Cancel();
        IsPaused = false;
        OverallProgress = 0;
        CurrentFileProgress = 0;
        CurrentFileName = string.Empty;
        ProgressText = progressText;
        LastReport = "нет отчёта";
        ImportSummary = "Сначала покажите медиа для текущего телефона.";

        foreach (var item in MediaItems)
        {
            item.PropertyChanged -= OnItemSelectionChanged;
        }

        MediaItems.Clear();
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectionSummary));
        UpdateCommands();
    }

    private static bool IsSameDeviceSession(DeviceInfo? previous, DeviceInfo? next)
    {
        if (previous is null || next is null)
        {
            return previous is null && next is null;
        }

        return string.Equals(previous.DeviceId, next.DeviceId, StringComparison.Ordinal)
            && string.Equals(previous.SerialNumber, next.SerialNumber, StringComparison.Ordinal)
            && string.Equals(previous.DisplayName, next.DisplayName, StringComparison.Ordinal)
            && previous.IsTrusted == next.IsTrusted;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void AddLog(string message)
    {
        LogLines.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss} | {message}");
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var units = new[] { "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unit = -1;
        do
        {
            value /= 1024;
            unit++;
        }
        while (value >= 1024 && unit < units.Length - 1);

        return $"{value:0.##} {units[unit]}";
    }
}
