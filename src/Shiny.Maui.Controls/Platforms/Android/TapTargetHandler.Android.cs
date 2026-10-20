using Android.OS;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Shiny.Maui.Controls.Infrastructure;

public partial class TapTargetHandler
{
    partial void ApplyButtonSemantics()
    {
        // MAUI installs a delegate of its own for hints, descriptions and ids, and replaces it when the
        // semantics change, so ours wraps whatever is there rather than displacing it. The mapping took
        // ours off before MAUI's ran, so this leaves exactly one of ours on top.
        var current = ViewCompat.GetAccessibilityDelegate(this.PlatformView);
        if (current is ButtonAccessibilityDelegate)
            return;

        ViewCompat.SetAccessibilityDelegate(this.PlatformView, new ButtonAccessibilityDelegate(this, current));
    }

    partial void RemoveButtonSemantics() => Unwrap(this.PlatformView);

    protected override void DisconnectHandler(ContentViewGroup platformView)
    {
        // MAUI clears its delegate before this runs, but only when it is on top, and ours is. Put
        // MAUI's back and clear it the way MAUI would have, so neither outlives the handler.
        Unwrap(platformView);
        if (ViewCompat.GetAccessibilityDelegate(platformView) is MauiAccessibilityDelegateCompat maui)
        {
            maui.Handler = null;
            ViewCompat.SetAccessibilityDelegate(platformView, (AccessibilityDelegateCompat?)null);
        }
        base.DisconnectHandler(platformView);
    }

    static void Unwrap(AView view)
    {
        if (ViewCompat.GetAccessibilityDelegate(view) is ButtonAccessibilityDelegate ours)
            ViewCompat.SetAccessibilityDelegate(view, ours.Inner);
    }

    /// <summary>
    /// Announces the view as a button and answers TalkBack's double-tap (and an automation click) by
    /// pressing it. The click is handled here rather than with a click listener, which a real finger tap
    /// would also fire, on top of the tap gesture.
    /// </summary>
    sealed class ButtonAccessibilityDelegate(TapTargetHandler handler, AccessibilityDelegateCompat? inner) : AccessibilityDelegateCompat
    {
        /// <summary>The delegate this one wraps (MAUI's, or none), put back when ours comes off.</summary>
        public AccessibilityDelegateCompat? Inner => inner;

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
