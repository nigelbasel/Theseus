using System.Globalization;

namespace Theseus.Core.Budgets;

/// <summary>Dollar amounts as the card shows them: two places, invariant culture, so a stop reads the same on any machine.</summary>
public static class Money
{
    /// <summary>The amount as <c>$1.23</c>.</summary>
    public static string Usd(decimal amount) => "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
}
