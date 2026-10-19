using UIKit;

namespace Shiny.Maui.Controls.Infrastructure;

public partial class TapTargetHandler
{
    partial void ApplyButtonSemantics()
    {
        // VoiceOver's double-tap lands as a touch at the element's centre, which the tap gesture
        // already answers, so the trait is all that is missing. MAUI's semantics mapping has just set
        // the label (and the header trait, if any), so this adds to the traits rather than replacing them.
        this.PlatformView.IsAccessibilityElement = true;
        this.PlatformView.AccessibilityTraits |= UIAccessibilityTrait.Button;
    }
}
