using System.Windows;

namespace Theseus.App.Controls;

/// <summary>
/// Why a menu item or button is disabled (baseline W13, W31; checklist G61, G157). The <c>ExplainsWhenOff</c> styles show
/// it as the tooltip while the element is disabled, and the element's <c>Tag</c> as the tooltip while it is enabled, so a
/// command that does not apply stays in place, greyed, and says why.
/// </summary>
public static class OffReason
{
    /// <summary>The reason shown while the element is disabled.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(OffReason), new PropertyMetadata(null));

    /// <summary>Gets <see cref="TextProperty"/>.</summary>
    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    /// <summary>Sets <see cref="TextProperty"/>.</summary>
    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);
}
