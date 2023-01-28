using System.Collections.Generic;

namespace required_1_on_1_dependants.Chinook;

public sealed class Playlist : BaseEntity
{
    public string? Name { get; set; }

    public ICollection<Track>? Tracks { get; set; }
}