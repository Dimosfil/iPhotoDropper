using System.ComponentModel;
using System.Runtime.CompilerServices;
using iPhotoDropper.Core.Models;

namespace iPhotoDropper.App.ViewModels;

public sealed class TransferItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    public required MediaItem Source { get; init; }

    public string FileName => Source.FileName;
    public string TypeText => Source.Kind == MediaKind.Photo ? "Фото" : "Видео";
    public string SizeText => $"{Source.SizeBytes / 1024.0 / 1024.0:F2} MB";
    public string? CapturedText => Source.CapturedAt?.ToLocalTime().ToString("g");

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
