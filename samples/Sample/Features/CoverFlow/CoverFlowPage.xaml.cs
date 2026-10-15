namespace Sample.Features.CoverFlow;

public partial class CoverFlowPage : ContentPage
{
    public CoverFlowPage()
    {
        InitializeComponent();
        SampleSourceCode.Attach(this);
    }

    void OnPrevious(object? sender, EventArgs e) => Flow.Previous();

    void OnNext(object? sender, EventArgs e) => Flow.Next();
}
