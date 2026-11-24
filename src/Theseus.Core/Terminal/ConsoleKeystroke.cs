namespace Theseus.Core.Terminal;

/// <summary>One key event written into a console's input buffer.</summary>
/// <param name="Character">The character the key produces.</param>
/// <param name="VirtualKey">The virtual key code, or zero for a plain character.</param>
/// <param name="IsDown">Whether the key goes down, rather than up.</param>
/// <param name="WithControl">Whether Ctrl is held.</param>
public sealed record ConsoleKeystroke(char Character, ushort VirtualKey, bool IsDown, bool WithControl = false);
