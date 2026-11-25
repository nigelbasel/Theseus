namespace Theseus.Core.Links;

/// <summary>How a Chains row shows under the panel's search and filters.</summary>
public enum ChainVisibility
{
    /// <summary>It is kept by the search and every filter.</summary>
    Shown,

    /// <summary>It is not kept itself, but a row below it is, so it stays to show where that row sits.</summary>
    Context,

    /// <summary>Neither it nor anything below it is kept.</summary>
    Hidden,
}
