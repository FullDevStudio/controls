using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;
using Shiny.Maui.Controls.ImageEditor;
using Shiny.Maui.Controls.Media;

namespace Sample.Features.Media;

[ShellMap<MediaPickerPage>]
public partial class MediaPickerViewModel : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<MediaPickerItem> photos = new();

    [ObservableProperty]
    bool allowGallery = true;

    [ObservableProperty]
    bool allowCamera = true;

    [ObservableProperty]
    bool allowPhotoEdit = true;

    [ObservableProperty]
    bool showAsCarousel = true;

    [ObservableProperty]
    int maxPhotos = 5;

    [ObservableProperty]
    double compressionQuality = 85;

    [ObservableProperty]
    double itemSpacing = 8;

    [ObservableProperty]
    ObservableCollection<MediaPickerItem> customPhotos = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputFormat))]
    bool usePng;

    public ImageExportFormat OutputFormat => this.UsePng ? ImageExportFormat.Png : ImageExportFormat.Jpeg;

    public int CompressionQualityPercent => (int)this.CompressionQuality;

    partial void OnCompressionQualityChanged(double value)
        => OnPropertyChanged(nameof(CompressionQualityPercent));

    /// <summary>
    /// Populates the picker from bundled images so the "has photos" state can be seen
    /// without granting photo-library access (handy on a fresh simulator).
    /// </summary>
    [RelayCommand]
    async Task LoadSamplePhotos()
    {
        this.Photos.Clear();
        for (var i = 1; i <= 3; i++)
        {
            await using var src = await FileSystem.OpenAppPackageFileAsync($"sample_photo{i}.jpg");
            using var ms = new MemoryStream();
            await src.CopyToAsync(ms);
            // The first stands in for a photo that is already saved, so it can't be removed.
            this.Photos.Add(new MediaPickerItem(ms.ToArray(), 900, 1200, "image/jpeg") { CanRemove = i > 1 });
        }
        OnPropertyChanged(nameof(this.Photos));
    }

    [RelayCommand]
    void ClearPhotos() => this.Photos.Clear();
}
