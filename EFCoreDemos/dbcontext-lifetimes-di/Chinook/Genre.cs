using System.Collections.Generic;

namespace dbcontext_lifetimes_di.Chinook;

public class Genre : BaseEntity
{
    public Genre()
    {
        Tracks = new HashSet<Track>();
    }

    public string Name { get; set; }
    public virtual ICollection<Track> Tracks { get; set; }
}