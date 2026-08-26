using Theseus.Core.Usage;

namespace Theseus.Tests.Usage;

[TestFixture]
public class ClaudeAccountReaderTests
{
    private string _directory = string.Empty;
    private string _config = string.Empty;
    private string _credentials = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-account-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _config = Path.Combine(_directory, ".claude.json");
        _credentials = Path.Combine(_directory, ".credentials.json");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Read_NamesTheSignedInAccount()
    {
        WriteConfig();
        WriteCredentials();

        var account = Read();

        Assert.That(account.EmailAddress, Is.EqualTo("nigel@nurtur.tech"));
        Assert.That(account.OrganizationName, Is.EqualTo("nigel@nurtur.tech's Organization"));
        Assert.That(account.SubscriptionType, Is.EqualTo("max"));
        Assert.That(account.Describe, Is.EqualTo("nigel@nurtur.tech · max"));
    }

    [Test]
    public void Read_TakesNothingButIdentityFromTheConfiguration()
    {
        File.WriteAllText(_config, """
            {
              "oauthAccount": {
                "accountUuid": "11111111-2222-3333-4444-555555555555",
                "emailAddress": "nigel@nurtur.tech",
                "organizationUuid": "66666666-7777-8888-9999-000000000000",
                "organizationName": "Nurtur"
              }
            }
            """);

        var account = Read();

        Assert.That(account.Describe, Does.Not.Contain("1111"));
        Assert.That(account.Describe, Does.Not.Contain("6666"),
            "identifiers can act on the account's behalf and have no business on a status bar");
    }

    [Test]
    public void Read_CopesWithTheDuplicateKeysTheRealConfigurationContains()
    {
        File.WriteAllText(_config, """
            {
              "projects": { "C:/dev/x": {}, "c:/dev/x": {} },
              "oauthAccount": { "emailAddress": "nigel@nurtur.tech" }
            }
            """);

        Assert.That(Read().EmailAddress, Is.EqualTo("nigel@nurtur.tech"),
            "paths differing only in case appear twice in the real file");
    }

    [Test]
    public void Read_SaysNothingWhenNobodyIsSignedIn()
    {
        var account = Read();

        Assert.That(account.IsKnown, Is.False);
        Assert.That(account.Describe, Is.Empty);
    }

    [Test]
    public void Read_StillNamesTheAccountWhenTheCredentialsCannotBeRead()
    {
        WriteConfig();

        var account = Read();

        Assert.That(account.EmailAddress, Is.EqualTo("nigel@nurtur.tech"));
        Assert.That(account.SubscriptionType, Is.Null);
        Assert.That(account.Describe, Is.EqualTo("nigel@nurtur.tech"));
    }

    [Test]
    public void Read_FallsBackRatherThanThrowingOnAConfigurationItCannotParse()
    {
        File.WriteAllText(_config, "{ half a file");

        Assert.That(Read().IsKnown, Is.False);
    }

    private ClaudeAccount Read() => new ClaudeAccountReader(_config, _credentials).Read();

    private void WriteConfig() => File.WriteAllText(_config, """
        {
          "oauthAccount": {
            "emailAddress": "nigel@nurtur.tech",
            "organizationName": "nigel@nurtur.tech's Organization"
          }
        }
        """);

    private void WriteCredentials() => File.WriteAllText(_credentials, """
        {"claudeAiOauth":{"accessToken":"secret","subscriptionType":"max","rateLimitTier":"default_claude_max_20x"}}
        """);
}
