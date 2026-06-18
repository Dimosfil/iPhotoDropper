using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using iPhotoDropper.App.ViewModels;
using iPhotoDropper.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace iPhotoDropper.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow(TransferViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "iPhotoDropper";
        ConfigureWindow();

        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.MediaItems.CollectionChanged += OnViewModelCollectionChanged;
        ViewModel.LogLines.CollectionChanged += OnViewModelCollectionChanged;
        ViewModel.ExistingFileConflictResolver = ResolveExistingFileConflictAsync;
        UpdateUi();
    }

    public TransferViewModel ViewModel { get; }

    private string _destinationFilesText = "Список скопированных файлов обновляется...";
    private string? _destinationFilesFolder;
    private bool _destinationFilesRefreshRunning;
    private DateTimeOffset _lastDestinationFilesRefresh = DateTimeOffset.MinValue;
    private ExistingFileAction? _existingFileActionForAll;
    private bool _uiUpdateQueued;
    private bool _updatingUi;

    private void ConfigureWindow()
    {
        AppWindow.Resize(new SizeInt32(1180, 780));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }
    }

    private async void OnBrowseDestination(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            ViewModel.DestinationFolder = folder.Path;
            UpdateUi();
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshDevicesAsync();
        UpdateUi();
    }

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        var scanTask = ViewModel.ScanAsync();
        UpdateUi();
        await scanTask;
        UpdateUi();
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        _existingFileActionForAll = null;
        if (ViewModel.MediaItems.Count == 0)
        {
            await ViewModel.ScanAsync();
        }

        if (!ViewModel.ImportCommand.CanExecute(null))
        {
            UpdateUi();
            return;
        }

        if (!await ConfirmImportAsync())
        {
            UpdateUi();
            return;
        }

        await ViewModel.ImportAsync();
        _lastDestinationFilesRefresh = DateTimeOffset.MinValue;
        UpdateUi();
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        ViewModel.PauseTransfer();
        UpdateUi();
    }

    private void OnResumeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ResumeTransfer();
        UpdateUi();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelTransfer();
        UpdateUi();
    }

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(ViewModel.LogFolderPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = ViewModel.LogFolderPath,
            UseShellExecute = true
        });
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        ViewModel.CopyPhotos = PhotosCheckBox.IsChecked == true;
        ViewModel.CopyVideos = VideosCheckBox.IsChecked == true;
        ViewModel.CopyOnlyNew = CopyOnlyNewCheckBox.IsChecked == true;
        ViewModel.OrganizeByDate = OrganizeByDateCheckBox.IsChecked == true;
        UpdateUi();
    }

    private void OnMaxSizeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        ViewModel.MaxFileSizeMbText = string.IsNullOrWhiteSpace(MaxSizeTextBox.Text)
            ? null
            : MaxSizeTextBox.Text.Trim();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        QueueUpdateUi();
    }

    private void OnViewModelCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueUpdateUi();
    }

    private void QueueUpdateUi()
    {
        if (_uiUpdateQueued)
        {
            return;
        }

        _uiUpdateQueued = true;
        Root.DispatcherQueue.TryEnqueue(() =>
        {
            _uiUpdateQueued = false;
            UpdateUi();
        });
    }

    private void UpdateUi()
    {
        _updatingUi = true;
        try
        {
            DeviceStatusText.Text = ViewModel.DeviceStatus;
            DeviceDetailsTextBlock.Text = RenderDeviceDetails();
            LogFolderTextBlock.Text = $"Папка логов: {ViewModel.LogFolderPath}";
            DestinationTextBlock.Text = $"Папка: {ViewModel.DestinationFolder}";
            SelectionSummaryTextBlock.Text = ViewModel.MediaItems.Count == 0
                ? "Медиа еще не просканированы"
                : ViewModel.SelectionSummary;

            ProgressTextBlock.Text = $"Статус: {ViewModel.ProgressText}";
            CurrentFileTextBlock.Text = string.IsNullOrWhiteSpace(ViewModel.CurrentFileName)
                ? "Текущий файл: нет"
                : $"Текущий файл: {ViewModel.CurrentFileName}";
            ProgressBarTextBlock.Text = $"{ViewModel.OverallProgress}% общий / {ViewModel.CurrentFileProgress}% файл";
            OverallProgressLabelTextBlock.Text = $"Общий прогресс: {ViewModel.OverallProgress}%";
            OverallProgressBarTextBlock.Text = RenderProgressBar(ViewModel.OverallProgress);
            CurrentFileProgressLabelTextBlock.Text = $"Прогресс файла: {ViewModel.CurrentFileProgress}%";
            CurrentFileProgressBarTextBlock.Text = RenderProgressBar(ViewModel.CurrentFileProgress);
            CurrentFileProgressDetailsTextBlock.Text = string.IsNullOrWhiteSpace(ViewModel.CurrentFileName)
                ? "Текущий файл: нет"
                : $"Текущий файл: {ViewModel.CurrentFileName} | {ViewModel.ProgressText}";
            LastReportTextBlock.Text = $"Отчет: {ViewModel.LastReport}{Environment.NewLine}{ViewModel.ImportSummary}";

            PhotosCheckBox.IsChecked = ViewModel.CopyPhotos;
            VideosCheckBox.IsChecked = ViewModel.CopyVideos;
            CopyOnlyNewCheckBox.IsChecked = ViewModel.CopyOnlyNew;
            OrganizeByDateCheckBox.IsChecked = ViewModel.OrganizeByDate;
            SetTextIfChanged(MaxSizeTextBox, ViewModel.MaxFileSizeMbText ?? string.Empty);

            if (!ViewModel.IsBusy)
            {
                SetTextIfChanged(MediaFilesTextBox, RenderMedia());
                RefreshDestinationFilesIfNeeded();
                SetTextIfChanged(DestinationFilesTextBox, _destinationFilesText);
                SetTextIfChanged(ScanLogTextBox, RenderLog());
                ScanLogTextBox.SelectionStart = ScanLogTextBox.Text.Length;
            }

            var importControlsEnabled = ViewModel.IsDeviceReady && !ViewModel.IsBusy;
            var scanButtonText = ViewModel.IsScanning ? "Сканируем..." : "Показать медиа";
            var mediaRefreshButtonText = ViewModel.IsScanning ? "Скан..." : "Обновить";
            ScanButtonTextBlock.Text = scanButtonText;
            MediaRefreshButtonTextBlock.Text = mediaRefreshButtonText;
            ScanButtonBusyTextBlock.Visibility = ViewModel.IsScanning ? Visibility.Visible : Visibility.Collapsed;
            MediaRefreshBusyTextBlock.Visibility = ViewModel.IsScanning ? Visibility.Visible : Visibility.Collapsed;
            PhotosCheckBox.IsEnabled = importControlsEnabled;
            VideosCheckBox.IsEnabled = importControlsEnabled;
            CopyOnlyNewCheckBox.IsEnabled = importControlsEnabled;
            OrganizeByDateCheckBox.IsEnabled = importControlsEnabled;
            MaxSizeTextBox.IsEnabled = importControlsEnabled;
            MediaRefreshButton.IsEnabled = ViewModel.ScanCommand.CanExecute(null);
            ScanButton.IsEnabled = ViewModel.ScanCommand.CanExecute(null);
            ImportButton.IsEnabled = ViewModel.ImportCommand.CanExecute(null);
            PauseButton.IsEnabled = ViewModel.PauseCommand.CanExecute(null);
            ResumeButton.IsEnabled = ViewModel.ResumeCommand.CanExecute(null);
            CancelButton.IsEnabled = ViewModel.CancelCommand.CanExecute(null);
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private async Task<ExistingFileAction> ResolveExistingFileConflictAsync(ExistingFileConflict conflict, CancellationToken token)
    {
        if (_existingFileActionForAll is { } actionForAll)
        {
            return actionForAll;
        }

        var completion = new TaskCompletionSource<ExistingFileAction>();
        if (!Root.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var applyToAllCheckBox = new CheckBox
                {
                    Content = "Для всех конфликтов в этом импорте"
                };
                var details = new TextBlock
                {
                    Text =
                        $"Уже есть: {Path.GetFileName(conflict.DestinationPath)}\n" +
                        $"В папке: {FormatBytes(conflict.ExistingSizeBytes)}, изменен {conflict.ExistingModifiedAt.LocalDateTime:g}\n" +
                        $"С iPhone: {conflict.SourceFileName}, {FormatBytes(conflict.SourceSizeBytes)}",
                    TextWrapping = TextWrapping.Wrap
                };
                var panel = new StackPanel
                {
                    Spacing = 12
                };
                panel.Children.Add(details);
                panel.Children.Add(applyToAllCheckBox);

                var dialog = new ContentDialog
                {
                    XamlRoot = Root.XamlRoot,
                    Title = "Файл уже существует",
                    Content = panel,
                    PrimaryButtonText = "Заменить",
                    SecondaryButtonText = "Пропустить",
                    DefaultButton = ContentDialogButton.Secondary
                };

                var result = await dialog.ShowAsync();
                var action = result == ContentDialogResult.Primary
                    ? ExistingFileAction.Replace
                    : ExistingFileAction.Skip;

                if (applyToAllCheckBox.IsChecked == true)
                {
                    _existingFileActionForAll = action;
                }

                completion.TrySetResult(action);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }))
        {
            return ExistingFileAction.Skip;
        }

        using var registration = token.Register(() => completion.TrySetCanceled(token));
        return await completion.Task;
    }

    private async Task<bool> ConfirmImportAsync()
    {
        var selectedBytes = ViewModel.SelectedBytes;
        var freeSpace = TryGetAvailableFreeSpace(ViewModel.DestinationFolder);
        var freeSpaceText = freeSpace is null ? "не удалось определить" : FormatBytes(freeSpace.Value);
        var hasEnoughSpace = freeSpace is null || freeSpace.Value >= selectedBytes;
        var spaceStatus = hasEnoughSpace
            ? "Места достаточно для выбранных файлов."
            : "Свободного места меньше, чем требуется для выбранных файлов.";

        var details = new TextBlock
        {
            Text =
                $"Папка: {ViewModel.DestinationFolder}\n" +
                $"Выбрано: {ViewModel.SelectedCount} файлов\n" +
                $"Требуется: {FormatBytes(selectedBytes)}\n" +
                $"Свободно на диске: {freeSpaceText}\n\n" +
                spaceStatus,
            TextWrapping = TextWrapping.Wrap
        };

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "Начать импорт?",
            Content = details,
            PrimaryButtonText = "Да",
            SecondaryButtonText = "Нет",
            DefaultButton = ContentDialogButton.Secondary,
            IsPrimaryButtonEnabled = hasEnoughSpace
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static long? TryGetAvailableFreeSpace(string destinationFolder)
    {
        try
        {
            var fullPath = Path.GetFullPath(destinationFolder);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
        }
        catch
        {
            return null;
        }
    }

    private string RenderDeviceDetails()
    {
        if (ViewModel.SelectedDevice is null)
        {
            return "iPhone не найден. Подключите USB, разблокируйте iPhone и нажмите \"Доверять\" на телефоне.";
        }

        var device = ViewModel.SelectedDevice;
        var source = device.DeviceId.StartsWith("mtp:", StringComparison.Ordinal)
            ? "реальный iPhone через USB/MTP"
            : "mock-источник";

        return string.Join(Environment.NewLine, new[]
        {
            $"Устройство: {device.DisplayName}",
            $"Источник: {source}",
            $"Доступ: {(device.IsTrusted ? "есть" : "нет, проверьте trust на iPhone")}",
            $"Транспорт: {device.Transport ?? "USB"}",
            $"Серийный номер: {device.SerialNumber ?? "не указан"}"
        });
    }

    private string RenderMedia()
    {
        if (ViewModel.MediaItems.Count == 0)
        {
            return "Медиа iPhone: нет данных. Нажмите \"Показать медиа\".";
        }

        var totalBytes = ViewModel.MediaItems.Sum(x => Math.Max(0, x.Source.SizeBytes));
        var lines = ViewModel.MediaItems
            .Take(1000)
            .Select((x, index) => $"{index + 1,3}. {x.FileName,-32} {x.TypeText,-8} {x.SizeText,10} {x.CapturedText}");

        return $"Медиа iPhone: {ViewModel.MediaItems.Count} файлов / {FormatBytes(totalBytes)}" +
            Environment.NewLine +
            string.Join(Environment.NewLine, lines);
    }

    private void RefreshDestinationFilesIfNeeded()
    {
        var folder = ViewModel.DestinationFolder;
        var now = DateTimeOffset.UtcNow;
        if (_destinationFilesRefreshRunning)
        {
            return;
        }

        var folderChanged = !string.Equals(_destinationFilesFolder, folder, StringComparison.OrdinalIgnoreCase);
        if (!folderChanged && now - _lastDestinationFilesRefresh < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _destinationFilesFolder = folder;
        _lastDestinationFilesRefresh = now;
        _destinationFilesRefreshRunning = true;

        _ = Task.Run(() => RenderDestinationFilesSnapshot(folder))
            .ContinueWith(task =>
            {
                var text = task.Exception is null
                    ? task.Result
                    : $"Не удалось прочитать папку назначения: {task.Exception.GetBaseException().Message}";

                Root.DispatcherQueue.TryEnqueue(() =>
                {
                    _destinationFilesText = text;
                    _destinationFilesRefreshRunning = false;
                    SetTextIfChanged(DestinationFilesTextBox, _destinationFilesText);
                });
            });
    }

    private static string RenderDestinationFilesSnapshot(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return "Папка назначения пока не создана.";
        }

        var files = Directory
            .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Select(file =>
            {
                var info = new FileInfo(file);
                return new
                {
                    Path = file,
                    info.Length,
                    info.LastWriteTimeUtc
                };
            })
            .ToArray();

        var totalFiles = files.Length;
        var totalBytes = files.Sum(file => Math.Max(0, file.Length));
        var shownFiles = files
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(300)
            .Select((file, index) =>
            {
                var relative = Path.GetRelativePath(folder, file.Path);
                return $"{index + 1,3}. {relative,-38} {FormatBytes(file.Length),10}";
            })
            .ToArray();

        var header = $"В папке назначения: {totalFiles} файлов / {FormatBytes(totalBytes)}";

        return shownFiles.Length == 0
            ? header + Environment.NewLine + "Скопированные файлы: пока пусто."
            : header + Environment.NewLine + "Скопированные файлы:" + Environment.NewLine + string.Join(Environment.NewLine, shownFiles);
    }

    private static void SetTextIfChanged(TextBox textBox, string text)
    {
        if (textBox.Text != text)
        {
            textBox.Text = text;
        }
    }

    private static string RenderProgressBar(int percent)
    {
        const int width = 28;
        var boundedPercent = Math.Clamp(percent, 0, 100);
        var filled = boundedPercent * width / 100;
        return "[" + new string('#', filled) + new string('.', width - filled) + $"] {boundedPercent,3}%";
    }

    private string RenderLog()
    {
        if (ViewModel.LogLines.Count == 0)
        {
            return "Журнал: пуст.";
        }

        return "Журнал:" + Environment.NewLine + string.Join(Environment.NewLine, ViewModel.LogLines.Take(500));
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
