namespace Theseus.Core.Settings;

/// <summary>Reads and writes the user's settings.</summary>
public interface ISettingsStore
{
    /// <summary>Reads the settings, falling back to the defaults for anything missing or unreadable.</summary>
    TheseusSettings Read();

    /// <summary>Writes the settings atomically.</summary>
    void Write(TheseusSettings settings);
}
