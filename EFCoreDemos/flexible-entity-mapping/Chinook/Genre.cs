using System.Collections.Generic;

namespace flexible_entity_mapping.Chinook;

public sealed class Genre : BaseEntity
{
    public Genre()
    {
        Tracks = new HashSet<Track>();
    }

    public string? Name { get; set; }
    public ICollection<Track>? Tracks { get; set; }
}