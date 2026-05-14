using System.Collections.Generic;

namespace projections_perf.Chinook;

public class MediaType : BaseEntity
{
    public MediaType()
    {
        Tracks = new HashSet<Track>();
    }

    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}