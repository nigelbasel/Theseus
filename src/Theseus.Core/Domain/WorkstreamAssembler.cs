using System.Globalization;

namespace Theseus.Core.Domain;

/// <summary>
/// Joins what the sessions write to what the board remembers. The plan file always wins on the
/// fields it owns, because a session that has just written one knows more about the work than
/// yesterday's board does.
/// </summary>
public static class WorkstreamAssembler
{
    /// <summary>Builds the board from the plans on disk and whatever sidecars exist for them.</summary>
    public static IReadOnlyList<Workstream> Assemble(
        IReadOnlyDictionary<string, SessionPlan> plans,
        IReadOnlyList<WorkstreamSidecar> sidecars)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(sidecars);

        var byKey = sidecars.ToDictionary(sidecar => sidecar.Key, StringComparer.OrdinalIgnoreCase);

        return plans
            .Select(entry => Build(entry.Key, entry.Value, byKey.GetValueOrDefault(entry.Key)))
            .OrderBy(workstream => workstream.BoardOrder)
            .ThenBy(workstream => workstream.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Reads a plan's <c>writtenAt</c>, which the writing session formats without an offset.</summary>
    public static DateTimeOffset ParseWrittenAt(string? writtenAt)
    {
        if (DateTime.TryParse(writtenAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
        }

        return DateTimeOffset.MinValue;
    }

    private static Workstream Build(string key, SessionPlan plan, WorkstreamSidecar? sidecar) =>
        new(
            Key: key,
            Title: plan.Title,
            ColorHex: plan.Color,
            ColorIsManual: sidecar?.ColorIsManual ?? true,
            WorkingDirectory: plan.Cwd,
            AddDirectories: plan.AddDirs,
            Prompt: plan.Prompt,
            BoardOrder: plan.Order,
            Column: sidecar?.Column ?? string.Empty,
            IsBlocked: sidecar?.IsBlocked ?? false,
            ReopenTomorrow: sidecar?.ReopenTomorrow ?? true,
            IsArchived: sidecar?.IsArchived ?? false,
            LastSessionId: plan.ResumeSessionId ?? sidecar?.LastSessionId,
            JiraKeys: JiraKeySelection.Visible(
                plan.JiraKeys, plan.Prompt, sidecar?.JiraKeys, sidecar?.RemovedJiraKeys),
            StatusDocumentPath: sidecar?.StatusDocumentPath,
            PlanWrittenUtc: ParseWrittenAt(plan.WrittenAt))
        {
            RemovedJiraKeys = sidecar?.RemovedJiraKeys ?? [],
            WindowGroup = sidecar?.WindowGroup,
            Area = sidecar?.Area,
            EveryStartPrompt = sidecar?.EveryStartPrompt,
            IsPaused = sidecar?.IsPaused ?? false,
        };
}
