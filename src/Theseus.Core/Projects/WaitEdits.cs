namespace Theseus.Core.Projects;

/// <summary>
/// What the owner's actions on a criterion's ask write to project.md (project-view-scale, git-22 for Nigel, 2026-11-24):
/// the ask taken off each criterion it covers, its other asks kept, or the ask moved to someone else. Each criterion is
/// found again by objective, place and text, and refused when the file no longer reads as shown.
/// </summary>
public static class WaitEdits
{
    /// <summary>Takes the asks off their criteria, keeping each criterion's other asks.</summary>
    public static string Clear(string text, IEnumerable<WaitCriterion> criteria) =>
        Rewrite(text, criteria, _ => null);

    /// <summary>Moves the asks to <paramref name="who"/>, or back to the owner when it is null, each in its criterion's own words.</summary>
    /// <param name="text">The project file's text.</param>
    /// <param name="criteria">The criteria and asks to move.</param>
    /// <param name="who">Whom they wait on now, or null for the owner; refused unless <see cref="WaitingAsk.IsWritableWho"/>.</param>
    public static string Reassign(string text, IEnumerable<WaitCriterion> criteria, string? who)
    {
        if (!string.IsNullOrWhiteSpace(who) && !WaitingAsk.IsWritableWho(who))
        {
            throw new ProjectEditRefusedException(
                $"\"{who}\" cannot be written as whom a criterion waits on: at most {WaitingAsk.LongestWho} characters, no colon or semicolon");
        }

        return Rewrite(text, criteria, criterion => string.IsNullOrWhiteSpace(who)
            ? new WaitingAsk(true, criterion.Asked)
            : new WaitingAsk(false, criterion.Asked) { On = who.Trim() });
    }

    private static string Rewrite(string text, IEnumerable<WaitCriterion> criteria, Func<WaitCriterion, WaitingAsk?> replace)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        foreach (var criterion in criteria)
        {
            var file = ProjectFileReader.Read("project", text).File
                       ?? throw new ProjectEditRefusedException("the project file does not read");
            var objective = file.Objectives.FirstOrDefault(each => each.Number == criterion.Objective)
                            ?? throw new ProjectEditRefusedException($"objective {criterion.Objective} is not in the file");
            var asks = objective.Criteria.ElementAtOrDefault(criterion.Index)?.Waiting?.Asks.ToList() ?? [];
            var at = asks.FindIndex(criterion.Is);

            if (at < 0)
            {
                throw new ProjectEditRefusedException($"criterion {criterion.Index + 1} of objective {criterion.Objective} no longer waits on \"{criterion.Ask}\"");
            }

            if (replace(criterion) is { } replacement)
            {
                asks[at] = replacement;
            }
            else
            {
                asks.RemoveAt(at);
            }

            text = ProjectFileEditor.SetCriterionNote(text, criterion.Objective, criterion.Index, criterion.Text, CriterionWaiting.Label,
                CriterionWaiting.Join(asks));
        }

        return text;
    }
}
