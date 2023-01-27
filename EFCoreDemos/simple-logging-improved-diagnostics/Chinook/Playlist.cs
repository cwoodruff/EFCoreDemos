using System.Collections.Generic;

namespace simple_logging_improved_diagnostics.Chinook;

public sealed class Playlist : BaseEntity
{
    public string? Name { get; set; }
    
    public ICollection<Track>? Tracks { get; set; }
}