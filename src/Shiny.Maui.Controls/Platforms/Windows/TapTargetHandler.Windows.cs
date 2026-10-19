using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Shiny.Maui.Controls.Infrastructure;

public partial class TapTargetHandler
{
    protected override ContentPanel CreatePlatformView()
        => new TapTargetPanel(this) { CrossPlatformLayout = this.VirtualView };

    partial void ApplyButtonSemantics()
    {
        // In the tab order, and pressed by Enter or Space, the way a XAML Button is.
        this.PlatformView.IsTabStop = this.VirtualView?.IsEnabled ?? true;
    }

    /// <summary>A content panel that UI Automation sees as a button, and that Enter or Space press.</summary>
    sealed partial class TapTargetPanel : ContentPanel
    {
        readonly TapTargetHandler handler;

        public TapTargetPanel(TapTargetHandler handler)
        {
            this.handler = handler;
            this.IsTabStop = true;
            this.UseSystemFocusVisuals = true;
            this.KeyDown += this.OnKeyDown;
        }

        internal void Invoke() => this.handler.Target?.Invoke();

        protected override AutomationPeer OnCreateAutomationPeer() => new TapTargetAutomationPeer(this);

        void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Space)
            {
                this.Invoke();
                e.Handled = true;
            }
        }
    }

    sealed partial class TapTargetAutomationPeer(TapTargetPanel owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetClassNameCore() => "Button";

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        protected override object? GetPatternCore(PatternInterface patternInterface)
            => patternInterface == PatternInterface.Invoke ? this : base.GetPatternCore(patternInterface);

        public void Invoke() => owner.Invoke();
    }
}
