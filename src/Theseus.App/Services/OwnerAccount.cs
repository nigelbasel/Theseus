using System.Runtime.InteropServices;
using System.Text;
using Theseus.Core.Projects;

namespace Theseus.App.Services;

/// <summary>The signed-in Windows account's names, for reading older waiting lines that name the owner.</summary>
public static class OwnerAccount
{
    private const int NameDisplay = 3;

    private static IReadOnlyList<string>? _names;

    /// <summary>The account's name, full name and first name, read once.</summary>
    public static IReadOnlyList<string> Names => _names ??= OwnerNames.From(Environment.UserName, DisplayName());

    // An account with no full name set, or one the call cannot resolve, gives none; the account's name still counts.
    private static string? DisplayName()
    {
        var size = 256;
        var buffer = new StringBuilder(size);

        return GetUserNameEx(NameDisplay, buffer, ref size) ? buffer.ToString() : null;
    }

    [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserNameEx(int nameFormat, StringBuilder nameBuffer, ref int size);
}
