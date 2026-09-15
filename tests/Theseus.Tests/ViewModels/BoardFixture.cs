using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A board over three fakes, so the view model can be tested at all.
///
/// The test project could not see `Theseus.App` until 2026-09-02, so `BoardViewModel` had no tests.
/// Two bugs in the selection work were then found by reading the code rather than by a failing test —
/// a refresh silently dropped every tick, and again every folded card — and both are the sort of
/// thing a test catches in a second and a person catches only by noticing the board misbehave.
///
/// The three stores are small enough to fake outright: five methods between them, all of them
/// reads and writes of plain records.
/// </summary>
internal sealed class BoardFixture
{
    private readonly FakePlanStore _plans = new();
    private readonly FakeBoardStore _board = new();
    private readonly FakeSettingsStore _settings = new();

    /// <summary>The board under test.</summary>
    public BoardViewModel Board { get; }

    /// <summary>Creates a board whose stages and areas are the ones given.</summary>
    public BoardFixture(IReadOnlyList<string>? lanes = null, IReadOnlyList<string>? areas = null)
    {
        _settings.Settings = TheseusSettings.Default with
        {
            Lanes = lanes ?? ["New", "In progress", "Done"],
            Areas = areas ?? ["Research", "Dev"],
        };

        Board = new BoardViewModel(_plans, _board, _settings);
    }

    /// <summary>Adds a workstream, which appears on the next refresh.</summary>
    public BoardFixture With(string key, string column = "New", string area = "Research")
    {
        _plans.Add(key, new SessionPlan(1, key, "#208DD7", @"C:\work", [], null,
            DateTimeOffset.UtcNow.ToString("O"), "Carry on."));

        _board.Add(new WorkstreamSidecar(key, column, IsBlocked: false, IsArchived: false,
            ColorIsManual: false, ReopenTomorrow: true, [], null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
            Area = area,
        });

        return this;
    }

    /// <summary>Gives a workstream's plan a new title, the way a session rewriting its plan would.</summary>
    public BoardFixture Retitle(string key, string title)
    {
        _plans.Add(key, _plans.TryRead(key)! with { Title = title });

        return this;
    }

    /// <summary>Moves a workstream to another stage in the board file.</summary>
    public BoardFixture MoveTo(string key, string column)
    {
        _board.Write([.. _board.Read().Select(sidecar =>
            string.Equals(sidecar.Key, key, StringComparison.OrdinalIgnoreCase) ? sidecar with { Column = column } : sidecar)]);

        return this;
    }

    /// <summary>Deletes a workstream's plan, the way deleting the file by hand would.</summary>
    public BoardFixture Without(string key)
    {
        _plans.Delete(key);

        return this;
    }

    /// <summary>The card for one workstream, or null when the board does not hold it.</summary>
    public WorkstreamCardViewModel? Card(string key) =>
        Board.Cards.FirstOrDefault(card =>
            string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase));

    private sealed class FakePlanStore : IPlanStore
    {
        private readonly Dictionary<string, SessionPlan> _plans = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string key, SessionPlan plan) => _plans[key] = plan;

        public IReadOnlyList<string> Keys() => [.. _plans.Keys];

        public string PathFor(string key) => $@"C:\plans\{key}.json";

        public SessionPlan? TryRead(string key) => _plans.GetValueOrDefault(key);

        public void Write(string key, SessionPlan plan) => _plans[key] = plan;

        public void Delete(string key) => _plans.Remove(key);
    }

    private sealed class FakeBoardStore : IBoardStore
    {
        private List<WorkstreamSidecar> _sidecars = [];

        public void Add(WorkstreamSidecar sidecar) => _sidecars.Add(sidecar);

        public IReadOnlyList<WorkstreamSidecar> Read() => _sidecars;

        public void Write(IReadOnlyList<WorkstreamSidecar> sidecars) => _sidecars = [.. sidecars];
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public TheseusSettings Settings { get; set; } = TheseusSettings.Default;

        public TheseusSettings Read() => Settings;

        public void Write(TheseusSettings settings) => Settings = settings;
    }
}
