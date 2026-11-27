using System.Text.Json.Serialization;

namespace Theseus.Core.Projects;

/// <summary>
/// One line of <c>piece-events.jsonl</c> (7.3): the history now_at and the criteria never kept, so time per stage, rework
/// after a failed check and the moment a piece finished can be worked out. The moment is stamped by Theseus, never by
/// the caller.
/// </summary>
/// <param name="At">When Theseus recorded it.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Project">The project.</param>
/// <param name="PieceId">The piece's stable id, or null when it has none yet.</param>
/// <param name="Slug">The piece's slug at the time, or null for an event on an objective.</param>
/// <param name="By">Who: the card key of the session that did it, or <see cref="Owner"/>.</param>
public sealed record PieceEvent(DateTimeOffset At, PieceEventKind Kind, string Project, string? PieceId, string? Slug, string By)
{
    /// <summary>What <see cref="By"/> says when the owner did it, on the board.</summary>
    public const string Owner = "owner";

    /// <summary>The stage entered or skipped.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Stage { get; init; }

    /// <summary>What the session said it was doing, or why it skipped.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }

    /// <summary>The objective a criterion belongs to, by its number.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Objective { get; init; }

    /// <summary>The criterion's number, from 1.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Criterion { get; init; }

    /// <summary>The verdict of a confirmed report.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Verdict { get; init; }

    /// <summary>The card a confirmed report came from, when not the one in <see cref="By"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Card { get; init; }

    /// <summary>The machine's UTC offset when it was recorded, as <c>+02:00</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Offset { get; init; }
}
