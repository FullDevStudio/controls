using Android.OS;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using AView = Android.Views.View;

namespace Shiny.Maui.Controls.Infrastructure;

public partial class TapTargetHandler
{
    partial void ApplyButtonSemantics()
    {
        // MAUI installs a delegate of its own for hints and headings, and replaces it when the
        // semantics change, so ours wraps whatever is there rather than displacing it.
        var current = ViewCompat.GetAccessibilityDelegate(this.PlatformView);
        if (current is ButtonAccessibilityDelegate)
            return;

        ViewCompat.SetAccessibilityDelegate(this.PlatformView, new ButtonAccessibilityDelegate(this, current));
    }

    /// <summary>
    /// Announces the view as a button and answers TalkBack's double-tap (and an automation click) by
    /// pressing it. The click is handled here rather than with a click listener, which a real finger tap
    /// would also fire, on top of the tap gesture.
    /// </summary>
    sealed class ButtonAccessibilityDelegate(TapTargetHandler handler, AccessibilityDelegateCompat? inner) : AccessibilityDelegateCompat
    {
        public override void OnInitializeAccessibilityNodeInfo(AView? host, AccessibilityNodeInfoCompat? info)
        {
            if (inner != null)
                inner.OnInitializeAccessibilityNodeInfo(host, info);
            else
                base.OnInitializeAccessibilityNodeInfo(host, info);

            if (info is null)
                return;

            info.ClassName = "android.widget.Button";
            info.Clickable = true;
            info.Enabled = handler.VirtualView?.IsEnabled ?? true;
            info.AddAction(AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionClick);
        }

        public override bool PerformAccessibilityAction(AView? host, int action, Bundle? args)
        {
            if (action == AccessibilityNodeInfoCompat.ActionClick && handler.Target is { } target)
            {
                target.Invoke();
                return true;
            }

            return inner?.PerformAccessibilityAction(host, action, args) ?? base.PerformAccessibilityAction(host, action, args);
        }
    }
}
