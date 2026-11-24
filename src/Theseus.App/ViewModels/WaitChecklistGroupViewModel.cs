using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One wait in the waits checklist, grouped by what it asks: its words, whom it waits on, and the objectives and criteria
/// it covers, each criterion a row under it when it covers several. Ticking it ticks them (git-22 for Nigel, 2026-11-24).
/// </summary>
public sealed class WaitChecklistGroupViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _isTicked;

    /// <summary>Creates the group; ticked when the checklist was opened for it.</summary>
    /// <param name="line">The line, with its own button for a wait Theseus acts on itself.</param>
    /// <param name="whom">"For you", "On the gateway team" or "For you, when phase 3 starts".</param>
    /// <param name="isTicked">Whether it starts ticked.</param>
    /// <param name="changed">Told when a tick in it changes.</param>
    public WaitChecklistGroupViewModel(WaitItemViewModel line, string whom, bool isTicked, Action changed)
    {
        Line = line;
        Whom = whom;
        _isTicked = isTicked;
        _changed = changed;
        Members = Item.Criteria.Count > 1
            ? [.. Item.Criteria.Select(criterion => new WaitChecklistMemberViewModel(criterion, isTicked, OnMemberChanged))]
            : [];
    }

    /// <summary>The line.</summary>
    public WaitItemViewModel Line { get; }

    /// <summary>The wait.</summary>
    public WaitItem Item => Line.Item;

    /// <summary>Its words.</summary>
    public string Text => Line.Text;

    /// <summary>Whom it waits on.</summary>
    public string Whom { get; }

    /// <summary>"Objectives 1.1, 1.2 · 3 criteria", or empty for a wait on no criterion.</summary>
    public string Covers => Item.Criteria.Count == 0
        ? string.Empty
        : $"{(Item.Objectives.Count == 1 ? "Objective" : "Objectives")} {string.Join(", ", Item.Objectives)} · "
          + (Item.Criteria.Count == 1 ? "1 criterion" : $"{Item.Criteria.Count} criteria");

    /// <summary>"For you · Objective 1.1 · 1 criterion", the row's second line.</summary>
    public string Detail => Covers.Length == 0 ? Whom : $"{Whom} · {Covers}";

    /// <summary>Whether it is a criterion's ask, which the command bar can act on.</summary>
    public bool IsCriterionWait => Item.Criteria.Count > 0;

    /// <summary>Its criteria as rows, when it covers more than one.</summary>
    public IReadOnlyList<WaitChecklistMemberViewModel> Members { get; }

    /// <summary>Whether it has rows under it.</summary>
    public bool HasMembers => Members.Count > 0;

    /// <summary>Ticked, unticked, or null while only some of its criteria are ticked.</summary>
    public bool? IsTicked
    {
        get => Members.Count == 0 ? _isTicked
            : Members.All(member => member.IsTicked) ? true
            : Members.Any(member => member.IsTicked) ? null
            : false;
        set
        {
            var ticked = value == true;

            if (Members.Count == 0)
            {
                _isTicked = ticked;
            }

            foreach (var member in Members)
            {
                member.SetQuietly(ticked);
            }

            Raise(nameof(IsTicked));
            _changed();
        }
    }

    /// <summary>Whether any of it is ticked.</summary>
    public bool IsAnyTicked => IsTicked != false;

    /// <summary>The criteria ticked in it.</summary>
    public IReadOnlyList<WaitCriterion> TickedCriteria => Members.Count == 0
        ? (_isTicked ? Item.Criteria : [])
        : [.. Members.Where(member => member.IsTicked).Select(member => member.Criterion)];

    /// <summary>The objectives of what is ticked in it.</summary>
    public IEnumerable<string> TickedObjectives => Members.Count == 0
        ? (_isTicked ? Item.Objectives : [])
        : TickedCriteria.Select(criterion => criterion.Objective).Distinct(StringComparer.Ordinal);

    private void OnMemberChanged()
    {
        Raise(nameof(IsTicked));
        _changed();
    }
}
