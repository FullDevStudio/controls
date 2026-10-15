using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace Sample.Features.CoverFlow;

public record Album(string Title, string Artist, int Year, Color Color);

[ShellMap<CoverFlowPage>(registerRoute: false)]
public partial class CoverFlowViewModel : ObservableObject
{
    public IReadOnlyList<Album> Albums { get; } =
    [
        new("Midnight Static", "The Lanterns", 2019, Color.FromArgb("#6366F1")),
        new("Paper Satellites", "Nova Harbour", 2021, Color.FromArgb("#0EA5E9")),
        new("Copper Coast", "Juniper Lane", 2017, Color.FromArgb("#F97316")),
        new("Glass Orchard", "Wren & Ash", 2023, Color.FromArgb("#10B981")),
        new("Slow Thunder", "Atlas Drift", 2015, Color.FromArgb("#EF4444")),
        new("Velvet Hours", "Moonlit Tram", 2020, Color.FromArgb("#A855F7")),
        new("Northern Lines", "Cedar Echo", 2018, Color.FromArgb("#14B8A6")),
        new("Saltwater Radio", "The Tidecallers", 2022, Color.FromArgb("#3B82F6")),
        new("Ember Season", "Fable Street", 2016, Color.FromArgb("#EAB308")),
        new("Quiet Engines", "Polar Bloom", 2024, Color.FromArgb("#EC4899")),
        new("Lantern Fields", "Harbour Kids", 2014, Color.FromArgb("#84CC16")),
        new("Afterglow City", "Neon Pines", 2025, Color.FromArgb("#F43F5E"))
    ];

    [ObservableProperty]
    int currentPosition = 3;

    [ObservableProperty]
    bool loop;

    [ObservableProperty]
    bool showReflection = true;

    [ObservableProperty]
    double sideAngle = 60;

    [ObservableProperty]
    double sideScale = 0.85;

    [ObservableProperty]
    string status = "Drag, flick or tap a side cover — tap the centre cover to select it";

    public string PositionLabel => $"{CurrentPosition + 1} / {Albums.Count}";

    partial void OnCurrentPositionChanged(int value) => OnPropertyChanged(nameof(PositionLabel));

    [RelayCommand]
    void Select(Album album) => Status = $"Selected {album.Title} by {album.Artist}";
}
