using System.Runtime.InteropServices;
using Theseus.App.Services;

namespace Theseus.Tests.Interop;

/// <summary>
/// The shape of the INPUT handed to SendInput.
///
/// This is the one defect in this project that no amount of exercising the application could have
/// found. `SendInput` compares the size it is given against its own idea of the structure and, when
/// they disagree, accepts nothing at all: no exception, no last-error worth reading, no typed
/// characters. The union was declared to fit only the keyboard member, making it 24 bytes and INPUT
/// 32 rather than the 40 an x64 Windows expects — and because the call site passes
/// `Marshal.SizeOf&lt;Input&gt;()`, the argument agreed with the wrong struct and nothing looked amiss.
///
/// Nothing was ever typed into any session, on any machine, by any build. It was diagnosed as a
/// permissions problem twice.
///
/// These assert absolute byte counts rather than comparing the code to itself. A test that said
/// `SizeOf&lt;Input&gt;() == SizeOf&lt;Input&gt;()` would have passed throughout.
/// </summary>
[TestFixture]
public class InputLayoutTests
{
    [Test]
    public void Input_IsTheFortyBytesX64WindowsExpects()
    {
        Assume.That(IntPtr.Size, Is.EqualTo(8), "the 40-byte figure is the 64-bit one");

        Assert.That(Marshal.SizeOf<TabNudge.Input>(), Is.EqualTo(40),
            "SendInput silently accepts nothing when the size disagrees with its own");
    }

    [Test]
    public void InputData_IsSizedToTheLargestMemberWindowsKnowsAbout()
    {
        Assert.That(Marshal.SizeOf<TabNudge.InputData>(), Is.EqualTo(32),
            "the union must be the size of a MOUSEINPUT, not of the keyboard member declared here");
    }

    [Test]
    public void KeyboardInput_MatchesTheKeyboardMemberOfTheUnion()
    {
        Assume.That(IntPtr.Size, Is.EqualTo(8));

        Assert.That(Marshal.SizeOf<TabNudge.KeyboardInput>(), Is.EqualTo(24),
            "two ushorts, two uints and a pointer, padded to eight");
    }

    [Test]
    public void InputData_IsWiderThanTheMemberDeclaredInIt()
    {
        // The bug in one line: a union sized to what it holds rather than to what it must fit.
        Assert.That(Marshal.SizeOf<TabNudge.InputData>(),
            Is.GreaterThan(Marshal.SizeOf<TabNudge.KeyboardInput>()));
    }
}
