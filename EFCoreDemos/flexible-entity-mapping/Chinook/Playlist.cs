using System.Collections.Generic;

namespace flexible_entity_mapping.Chinook;

public sealed class Playlist : BaseEntity
{
    public string? Name { get; set; }
    
    public ICollection<Track>? Tracks { get; set; }
}