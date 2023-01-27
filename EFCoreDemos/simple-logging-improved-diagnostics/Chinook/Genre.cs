using System.Collections.Generic;

namespace simple_logging_improved_diagnostics.Chinook;

public sealed class Genre : BaseEntity
{
    public Genre()
    {
        Tracks = new HashSet<Track>();
    }

    public string? Name { get; set; }
    public ICollection<Track>? Tracks { get; set; }
}