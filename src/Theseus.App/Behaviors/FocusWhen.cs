using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Theseus.App.Behaviors;

/// <summary>
/// Gives an element focus when a bound flag turns true, so a question asked in place starts on its safe answer
/// (baseline W43, finding B-m3, checklist G66): the drawer's delete confirmation focuses Keep.
/// </summary>
public static class FocusWhen
{
    /// <summary>The flag: focus moves to the element each time it turns true.</summary>
    public static readonly DependencyProperty IsTrueProperty = DependencyProperty.RegisterAttached(
        "IsTrue", typeof(bool), typeof(FocusWhen), new PropertyMetadata(false, OnIsTrueChanged));

    /// <summary>Gets the flag.</summary>
    public static bool GetIsTrue(DependencyObject element) => (bool)element.GetValue(IsTrueProperty);

    /// <summary>Sets the flag.</summary>
    public static void SetIsTrue(DependencyObject element, bool value) => element.SetValue(IsTrueProperty, value);

    private static void OnIsTrueChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement target || e.NewValue is not true)
        {
            return;
        }

        // The element becomes visible in the same change that sets the flag, and cannot take keyboard focus until it is
        // laid out. Logical focus is set now, so the window gives it keyboard focus whenever it is next active.
        DependencyObject? scope = FocusManager.GetFocusScope(target);

        if (scope is not null)
        {
            FocusManager.SetFocusedElement(scope, target);
        }

        target.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => target.Focus());
    }
}
