using Theseus.Core.Usage;

namespace Theseus.Tests.Usage;

[TestFixture]
public class ClaudeAccountSourceTests
{
    private string _directory = string.Empty;
    private string _config = string.Empty;
    private string _credentials = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-switch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _config = Path.Combine(_directory, ".claude.json");
        _credentials = Path.Combine(_directory, ".credentials.json");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Current_NamesTheAccountSignedInWhenTheBoardOpened()
    {
        SignIn("first@example.com");

        Assert.That(Source().Current.EmailAddress, Is.EqualTo("first@example.com"));
    }

    [Test]
    public void Refresh_NoticesTheSwitchToTheOtherAccount()
    {
        SignIn("first@example.com");
        var source = Source();

        SignIn("second@example.com");

        Assert.That(source.Refresh(), Is.True);
        Assert.That(source.Current.EmailAddress, Is.EqualTo("second@example.com"));
    }

    [Test]
    public void Refresh_SaysNothingChangedWhenNobodyHasSignedInOrOut()
    {
        SignIn("first@example.com");
        var source = Source();

        Assert.That(source.Refresh(), Is.False);
    }

    [Test]
    public void Refresh_DoesNotReportASwitchWhenTheFileIsRewrittenWithTheSameAccount()
    {
        SignIn("first@example.com");
        var source = Source();

        SignIn("first@example.com");

        Assert.That(source.Refresh(), Is.False,
            "the file is rewritten often; only a different account is worth telling anyone about");
    }

    [Test]
    public void Refresh_NoticesSigningOutAltogether()
    {
        SignIn("first@example.com");
        var source = Source();

        File.WriteAllText(_config, "{}");
        Touch(_config);

        Assert.That(source.Refresh(), Is.True);
        Assert.That(source.Current.IsKnown, Is.False);
    }

    [Test]
    public void Current_IsEmptyRatherThanMissingWhenNothingIsSignedIn()
    {
        Assert.That(Source().Current.Describe, Is.Empty);
    }

    private ClaudeAccountSource Source() => new(_config, _credentials);

    private void SignIn(string email)
    {
        File.WriteAllText(_config, $$$"""{"oauthAccount":{"emailAddress":"{{{email}}}"}}""");
        File.WriteAllText(_credentials, """{"claudeAiOauth":{"subscriptionType":"max"}}""");
        Touch(_config);
        Touch(_credentials);
    }

    private static void Touch(string path) =>
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(Random.Shared.Next(1, 100000)));
}
