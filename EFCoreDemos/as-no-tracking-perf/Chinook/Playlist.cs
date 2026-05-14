using System.Collections.Generic;

namespace as_no_tracking_perf.Chinook;

public class Playlist : BaseEntity
{
    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}