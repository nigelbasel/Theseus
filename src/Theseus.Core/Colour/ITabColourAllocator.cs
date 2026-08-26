namespace Theseus.Core.Colour;

/// <summary>Chooses the tab colour a newly created workstream is given.</summary>
public interface ITabColourAllocator
{
    /// <summary>
    /// Returns the palette colour that is most visually distinct from the colours already in use.
    /// The result is deterministic for a given state.
    /// </summary>
    string Allocate(ColourAllocationState state);
}
