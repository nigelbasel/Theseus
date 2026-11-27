using Theseus.Core.Settings;
using Theseus.Core.Setup;

namespace Theseus.Tests.Setup;

/// <summary>When the first-run setup opens by itself, and which of its steps it shows (first-run-setup, Nigel 2026-11-25).</summary>
[TestFixture]
public class FirstRunSetupTests
{
    private static readonly IReadOnlyList<ToolFinding> AllTools = [new("Claude Code", true, "Claude Code 2.1.300")];

    [Test]
    public void ItOpensAtStartup_WhenNoProjectsFolderIsSet()
    {
        Assert.That(FirstRunSetup.ShowsAtStartup(TheseusSettings.Default), Is.True);
    }

    [Test]
    public void ItDoesNotOpenAtStartup_WhenAProjectsFolderIsSet()
    {
        Assert.That(FirstRunSetup.ShowsAtStartup(TheseusSettings.Default with { ProjectsFolder = @"C:\projects" }), Is.False);
    }

    [Test]
    public void ItDoesNotOpenAtStartup_OnceItWasFinishedOrSkipped_EvenWithNoProjectsFolder()
    {
        Assert.That(FirstRunSetup.ShowsAtStartup(TheseusSettings.Default with { FirstRunSetupDone = true }), Is.False);
    }

    [Test]
    public void TheFolderTrustStep_IsShownWhileNoBaseFolderIsSet_BeforeTheTools()
    {
        var facts = new SetupFacts(true, true, true, [new("Node", false, "Node was not found")], FolderTrustIsOn: false);

        Assert.That(FirstRunSetup.Steps(facts), Is.EqualTo(new[] { SetupStep.FolderTrust, SetupStep.Tools }));
    }

    [Test]
    public void TheFolderTrustStep_IsLeftOut_OnceTheBaseFoldersAreSet()
    {
        Assert.That(FirstRunSetup.Steps(new SetupFacts(true, true, true, AllTools, FolderTrustIsOn: true)), Is.Empty);
    }

    [Test]
    public void OnlyTheMissingSteps_AreShown_InTheirOrder()
    {
        var facts = new SetupFacts(HasProjectsFolder: false, HookIsRegistered: true, GuardIsReady: false, AllTools);

        Assert.That(FirstRunSetup.Steps(facts), Is.EqualTo(new[] { SetupStep.Folders, SetupStep.Guards }));
    }

    [Test]
    public void EveryStep_IsShown_WhenNothingIsSetUp()
    {
        var facts = new SetupFacts(false, false, false, [new("Node", false, "Node was not found")]);

        Assert.That(FirstRunSetup.Steps(facts),
            Is.EqualTo(new[] { SetupStep.Folders, SetupStep.Hook, SetupStep.Guards, SetupStep.Tools }));
    }

    [Test]
    public void NoStep_IsShown_WhenEverythingIsSetUp()
    {
        Assert.That(FirstRunSetup.Steps(new SetupFacts(true, true, true, AllTools)), Is.Empty);
    }

    [Test]
    public void AStepAskedFor_IsShownFirst_EvenWhenItIsSetUp()
    {
        var facts = new SetupFacts(true, false, true, AllTools);

        Assert.That(FirstRunSetup.Steps(facts, SetupStep.Folders), Is.EqualTo(new[] { SetupStep.Folders, SetupStep.Hook }));
    }

    [Test]
    public void TheDoneFlag_SurvivesASaveAndARead()
    {
        var folder = Path.Combine(Path.GetTempPath(), "theseus-first-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var store = new SettingsStore(Path.Combine(folder, "settings.json"));
            store.Write(TheseusSettings.Default with { FirstRunSetupDone = true });

            Assert.That(new SettingsStore(Path.Combine(folder, "settings.json")).Read().FirstRunSetupDone, Is.True);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void AProjectsFolderInsideACodeRepo_IsWarnedAbout_ButOneThatIsItsOwnRepoIsNot()
    {
        var root = Path.Combine(Path.GetTempPath(), "theseus-first-run-" + Guid.NewGuid().ToString("N"));
        var repo = Path.Combine(root, "repo");
        var inside = Path.Combine(repo, "projects");
        var own = Path.Combine(root, "projects");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(inside);
        Directory.CreateDirectory(Path.Combine(own, ".git"));

        try
        {
            Assert.That(FirstRunSetup.ProjectsFolderWarning(inside), Does.Contain(repo));
            Assert.That(FirstRunSetup.ProjectsFolderWarning(own), Is.Null, "the suggested layout keeps the projects folder in a git repo of its own");
            Assert.That(FirstRunSetup.ProjectsFolderWarning(string.Empty), Is.Null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
