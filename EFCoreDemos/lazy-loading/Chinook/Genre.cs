using System.Collections.Generic;

namespace lazy_loading.Chinook;

public class Genre : BaseEntity
{
    public Genre()
    {
        Tracks = new HashSet<Track>();
    }

    public string? Name { get; set; }
    public virtual ICollection<Track>? Tracks { get; set; }
}