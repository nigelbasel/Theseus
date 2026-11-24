using Theseus.App.Text;

namespace Theseus.App.ViewModels;

/// <summary>
/// Gives each item of a menu an access key under the letter rule (baseline W26; checklist G58, G82, G83): the label's
/// first letter, else the first letter of another of its words, else another of its letters that G83 does not avoid. A
/// key is unique in its menu, and I and O are never used, since they read as 1 and 0 (G82). The project view's menus are
/// built from whatever a piece offers, so the letters are given as the menu is built rather than written into labels.
/// </summary>
public static class MenuAccessKeys
{
    private const string Avoided = "IOLGJPQY";

    /// <summary>The menu's items, each with its header.</summary>
    public static IReadOnlyList<MenuEntryViewModel> For(IEnumerable<PieceActionViewModel> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        HashSet<char> used = [];

        return [.. actions.Select(action => new MenuEntryViewModel(Header(action.SpokenLabel, used), action))];
    }

    /// <summary>The label with its key marked, taking a letter no earlier item took; escaped only, when none is left.</summary>
    public static string Header(string label, ISet<char> used)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(used);

        foreach (int at in Candidates(label))
        {
            char key = char.ToUpperInvariant(label[at]);

            if (used.Add(key))
            {
                return AccessKeyText.Escape(label[..at]) + "_" + AccessKeyText.Escape(label[at..]);
            }
        }

        return AccessKeyText.Escape(label);
    }

    // The first letter, then each later word's first letter, then any other letter G83 does not avoid; I and O never.
    private static IEnumerable<int> Candidates(string label)
    {
        List<int> letters = [.. Enumerable.Range(0, label.Length).Where(at => char.IsAsciiLetter(label[at]))];
        List<int> initials = [.. letters.Where(at => at == letters.FirstOrDefault(-1) || (at > 0 && !char.IsLetterOrDigit(label[at - 1])))];
        IEnumerable<int> others = letters.Except(initials).Where(at => !Avoided.Contains(char.ToUpperInvariant(label[at])));

        return initials.Concat(others).Where(at => char.ToUpperInvariant(label[at]) is not ('I' or 'O'));
    }
}
