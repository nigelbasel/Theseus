namespace Theseus.App.ViewModels;

/// <summary>A card "Link to…" can link to.</summary>
/// <param name="Key">Its key.</param>
/// <param name="Title">Its title, which the picker shows.</param>
public sealed record LinkTarget(string Key, string Title)
{
    /// <inheritdoc />
    public override string ToString() => Title;
}
