using Shiny.Maui.Controls;

namespace Sample.Features.Media;

public partial class VideoTextPage : ShinyContentPage
{
    public VideoTextPage()
    {
        this.InitializeComponent();
        SampleSourceCode.Attach(this);
    }

    void OnItalicToggled(object? sender, ToggledEventArgs e)
        => this.Video.FontAttributes = e.Value
            ? FontAttributes.Bold | FontAttributes.Italic
            : FontAttributes.Bold;
}
