using System.Collections.Generic;

namespace Demos.Chinook;

public sealed class Playlist : BaseEntity
{
    public string? Name { get; set; }

    public ICollection<Track>? Tracks { get; set; }
}