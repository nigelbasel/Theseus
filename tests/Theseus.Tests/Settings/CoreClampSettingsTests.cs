using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

/// <summary>The figures behind the cores-held-back warning, kept in the settings file (parked-cores spec AC 5).</summary>
[TestFixture]
public class CoreClampSettingsTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-clamp-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void TheDefaults_AreTheProvisionalOnesFromTheSpec()
    {
        var settings = new CoreClampSettings();

        Assert.That(settings.ParkedShare, Is.EqualTo(0.5));
        Assert.That(settings.RunningBusyPercent, Is.EqualTo(90));
        Assert.That(settings.HeldSamples, Is.EqualTo(3));
    }

    [Test]
    public void ASettingsFileWritten_BeforeTheWarningExisted_ReadsTheDefaults()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        store.Write(TheseusSettings.Default);
        var text = File.ReadAllText(Path.Combine(_directory, "settings.json"));
        File.WriteAllText(Path.Combine(_directory, "settings.json"),
            System.Text.RegularExpressions.Regex.Replace(text, @",\s*""coreClamp""\s*:\s*\{[^}]*\}", string.Empty));

        Assert.That(store.Read().CoreClamp, Is.EqualTo(new CoreClampSettings()));
    }

    [Test]
    public void FiguresWrittenToTheSettingsFile_AreRead()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));
        var tuned = new CoreClampSettings { ParkedShare = 0.75, RunningBusyPercent = 95, HeldSamples = 6 };

        store.Write(TheseusSettings.Default with { CoreClamp = tuned });

        Assert.That(store.Read().CoreClamp, Is.EqualTo(tuned));
    }
}
