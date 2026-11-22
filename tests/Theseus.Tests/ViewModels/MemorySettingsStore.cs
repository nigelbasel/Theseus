using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>Settings held in memory, so a test can build a view, "restart" by building another over the same store, and look.</summary>
internal sealed class MemorySettingsStore : ISettingsStore
{
    /// <summary>What is stored now.</summary>
    public TheseusSettings Settings { get; set; } = TheseusSettings.Default;

    public TheseusSettings Read() => Settings;

    /// <summary>How many times settings were written.</summary>
    public int Writes { get; private set; }

    public void Write(TheseusSettings settings)
    {
        Settings = settings;
        Writes++;
    }
}
