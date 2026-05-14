using System.Collections.Generic;

namespace dbcontext_lifetimes_di.Chinook;

public class Playlist : BaseEntity
{
    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}