using System.Collections.Generic;

namespace filtered_include.Chinook;

public sealed class Playlist : BaseEntity
{
    public string? Name { get; set; }

    public ICollection<Track>? Tracks { get; set; }
}