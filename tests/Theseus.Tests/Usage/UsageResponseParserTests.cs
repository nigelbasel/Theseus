using Theseus.Core.Usage;

namespace Theseus.Tests.Usage;

[TestFixture]
public class UsageResponseParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 9, 30, 0, TimeSpan.Zero);

    private const string RealResponse = """
        {
          "five_hour": {
            "utilization": 66.0,
            "resets_at": "2026-08-26T10:30:00.129654+00:00",
            "limit_dollars": null
          },
          "seven_day": {
            "utilization": 74.0,
            "resets_at": "2026-08-30T12:00:00.129676+00:00",
            "limit_dollars": null
          },
          "seven_day_opus": null,
          "nimbus_quill": { "utilization": 0.0, "resets_at": null },
          "extra_usage": { "is_enabled": false, "monthly_limit": 3750, "currency": "GBP" },
          "limits": [
            {
              "kind": "session", "group": "session", "percent": 66,
              "severity": "normal", "resets_at": "2026-08-26T10:30:00.129654+00:00",
              "scope": null, "is_active": false
            },
            {
              "kind": "weekly_all", "group": "weekly", "percent": 74,
              "severity": "normal", "resets_at": "2026-08-30T12:00:00.129676+00:00",
              "scope": null, "is_active": false
            },
            {
              "kind": "weekly_scoped", "group": "weekly", "percent": 76,
              "severity": "warning", "resets_at": "2026-08-30T12:00:00.129874+00:00",
              "scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null },
              "is_active": true
            }
          ]
        }
        """;

    [Test]
    public void Parse_ReadsTheFiveHourWindow()
    {
        var session = UsageResponseParser.Parse(RealResponse, Now).Session;

        Assert.That(session, Is.Not.Null);
        Assert.That(session!.Percent, Is.EqualTo(66));
        Assert.That(session.ResetsAtUtc!.Value.Hour, Is.EqualTo(10));
    }

    [Test]
    public void Parse_ReadsTheSevenDayWindow()
    {
        var weekly = UsageResponseParser.Parse(RealResponse, Now).Weekly;

        Assert.That(weekly, Is.Not.Null);
        Assert.That(weekly!.Percent, Is.EqualTo(74));
        Assert.That(weekly.ResetsAtUtc!.Value.Day, Is.EqualTo(30));
    }

    [Test]
    public void Parse_ReadsAModelScopedWindowAndNamesTheModel()
    {
        var scoped = UsageResponseParser.Parse(RealResponse, Now).Limits
            .Single(limit => limit.Kind == "weekly_scoped");

        Assert.That(scoped.ScopeLabel, Is.EqualTo("Fable"));
        Assert.That(scoped.Severity, Is.EqualTo("warning"));
        Assert.That(scoped.IsActive, Is.True);
    }

    [Test]
    public void Parse_PicksTheActiveWindowAsTheBindingOneEvenWhenItIsNotTheHighest()
    {
        var usage = UsageResponseParser.Parse("""
            {"limits":[
              {"kind":"weekly_all","group":"weekly","percent":90,"severity":"normal","is_active":false},
              {"kind":"session","group":"session","percent":20,"severity":"normal","is_active":true}]}
            """, Now);

        Assert.That(usage.Binding!.Kind, Is.EqualTo("session"));
    }

    [Test]
    public void Parse_FallsBackToTheHighestWindowWhenNoneIsMarkedActive()
    {
        Assert.That(UsageResponseParser.Parse(RealResponse, Now).Binding!.Kind,
            Is.EqualTo("weekly_scoped"));
    }

    [Test]
    public void Parse_FallsBackToTheNamedWindowsWhenTheLimitsArrayIsAbsent()
    {
        var usage = UsageResponseParser.Parse("""
            {"five_hour":{"utilization":40.0,"resets_at":"2026-08-26T10:30:00+00:00"},
             "seven_day":{"utilization":55.0,"resets_at":"2026-08-30T12:00:00+00:00"}}
            """, Now);

        Assert.That(usage.Session!.Percent, Is.EqualTo(40));
        Assert.That(usage.Weekly!.Percent, Is.EqualTo(55));
    }

    [Test]
    public void Parse_IgnoresTheShiftingSetOfUnnamedWindowsRatherThanChokingOnThem()
    {
        var kinds = UsageResponseParser.Parse(RealResponse, Now).Limits.Select(limit => limit.Kind);

        Assert.That(kinds, Is.EqualTo(new[] { "session", "weekly_all", "weekly_scoped" }));
    }

    [Test]
    public void Parse_SkipsALimitWithNoKindRatherThanInventingOne()
    {
        var usage = UsageResponseParser.Parse("""
            {"limits":[{"group":"weekly","percent":50},{"kind":"session","percent":10}]}
            """, Now);

        Assert.That(usage.Limits, Has.Count.EqualTo(1));
    }

    [Test]
    public void Parse_TreatsAMissingResetAsUnknownRatherThanNow()
    {
        var usage = UsageResponseParser.Parse("""
            {"limits":[{"kind":"session","percent":10,"resets_at":null}]}
            """, Now);

        Assert.That(usage.Limits.Single().ResetsAtUtc, Is.Null);
    }

    [Test]
    public void Parse_RecordsWhenTheReadingWasTaken()
    {
        Assert.That(UsageResponseParser.Parse(RealResponse, Now).RetrievedUtc, Is.EqualTo(Now));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("{not json")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("""{"limits":[]}""")]
    public void Parse_ReportsNoReadingRatherThanThrowing(string? body)
    {
        var usage = UsageResponseParser.Parse(body, Now);

        Assert.That(usage.HasReading, Is.False);
        Assert.That(usage.Binding, Is.Null);
    }
}
