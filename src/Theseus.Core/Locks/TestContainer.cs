namespace Theseus.Core.Locks;

/// <summary>A container carrying the Testcontainers label, as Docker lists it.</summary>
/// <param name="Id">Docker's short id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Image">The image it runs.</param>
/// <param name="CreatedUtc">When it was created.</param>
/// <param name="Status">Docker's status line, such as "Up 3 hours" or "Exited (0) 2 days ago".</param>
public sealed record TestContainer(string Id, string Name, string Image, DateTimeOffset CreatedUtc, string Status);
