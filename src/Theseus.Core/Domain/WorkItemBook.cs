using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Domain;

/// <summary>
/// <c>work-items.json</c>: the Jira issues and pull requests sessions have recorded against their cards,
/// with titles. Sessions record from the MCP server's threads while the board reads on the UI thread,
/// so every read and write takes the lock.
/// </summary>
public sealed class WorkItemBook
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private readonly List<WorkItem> _items;

    /// <summary>Opens the book over its file.</summary>
    public WorkItemBook(string path)
    {
        _path = path;
        _items = [.. Load(path)];
    }

    /// <summary>Raised after a change is saved, on the thread that made it.</summary>
    public event EventHandler? Changed;

    /// <summary>The card a title-only record belongs to: none.</summary>
    public const string NoCard = "";

    /// <summary>What has been recorded for one card, Jira issues first, each in the order recorded.</summary>
    public IReadOnlyList<WorkItem> For(string workstream)
    {
        lock (_gate)
        {
            return [.. _items.Where(item => item.Workstream.Length > 0 && Same(item.Workstream, workstream))
                .OrderBy(item => item.Kind).ThenBy(item => item.RecordedUtc)];
        }
    }

    /// <summary>
    /// The title an issue or pull request was last given, by any session for any card. A title belongs
    /// to the item, so every card carrying NDC-680 shows the title one session reported for it.
    /// </summary>
    public string? TitleOf(WorkItemKind kind, string id)
    {
        var key = kind == WorkItemKind.Jira ? id.Trim().ToUpperInvariant() : id.Trim().TrimStart('#', '!');

        lock (_gate)
        {
            return _items.Where(item => item.Kind == kind && Same(item.Id, key) && item.Title is not null)
                .MaxBy(item => item.RecordedUtc)?.Title;
        }
    }

    /// <summary>
    /// Records an item against a card, or updates its title and address if it is there already. A
    /// workstream of <see cref="NoCard"/> records a title alone. Says why not when the id is no use.
    /// </summary>
    public string? Record(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var id = item.Id.Trim();

        if (id.Length == 0)
        {
            return "id is required: the Jira key, or the pull request number.";
        }

        if (item.Kind == WorkItemKind.PullRequest && !int.TryParse(id.TrimStart('#', '!'), out _))
        {
            return "A pull request id is its number, such as 22174.";
        }

        id = item.Kind == WorkItemKind.Jira ? id.ToUpperInvariant() : id.TrimStart('#', '!');
        var title = string.IsNullOrWhiteSpace(item.Title) ? null : item.Title.Trim();
        var url = string.IsNullOrWhiteSpace(item.Url) ? null : item.Url.Trim();

        lock (_gate)
        {
            var index = _items.FindIndex(existing => Same(existing.Workstream, item.Workstream)
                                                     && existing.Kind == item.Kind && Same(existing.Id, id));
            var recorded = item with { Id = id, Title = title, Url = url };

            if (index >= 0)
            {
                _items[index] = recorded with
                {
                    Title = title ?? _items[index].Title,
                    Url = url ?? _items[index].Url,
                    RecordedUtc = title is null ? _items[index].RecordedUtc : item.RecordedUtc,
                };
            }
            else
            {
                _items.Add(recorded);
            }

            Save();
        }

        TheseusLog.Information("{Workstream} recorded {Kind} {Id}: {Title}", item.Workstream, item.Kind.ToString(), id, title ?? "no title");
        Changed?.Invoke(this, EventArgs.Empty);

        return null;
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<WorkItem> Load(string path)
    {
        var text = AtomicFile.TryReadAllText(path);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkItem>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The work items in {Path} could not be read, so none are shown", path);

            return [];
        }
    }

    private void Save()
    {
        try
        {
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(_items, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not save the work items to {Path}", _path);
        }
    }
}
