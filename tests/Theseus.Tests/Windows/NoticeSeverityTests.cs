using System.Windows.Threading;
using Theseus.App;
using Theseus.App.Controls;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>
/// The status bar's notice shows its severity by its glyph, not only its colour (baseline WB7; finding C17; W46;
/// checklist G147, G150): E946 for information, E7BA for a warning, E783 for an error.
/// </summary>
[TestFixture]
[NonParallelizable]
public class NoticeSeverityTests
{
    [TestCase(NoticeSeverity.Information, "")]
    [TestCase(NoticeSeverity.Warning, "")]
    [TestCase(NoticeSeverity.Error, "")]
    public void TheNoticeGlyph_FollowsTheSeverity(NoticeSeverity severity, string glyph) =>
        MainWindowFixture.Run((window, shell) =>
        {
            shell.Notice = "Something happened.";
            shell.NoticeSeverity = severity;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

            Assert.That(((GlyphText)window.FindName("NoticeGlyph")).Text, Is.EqualTo(glyph));
        });
}
