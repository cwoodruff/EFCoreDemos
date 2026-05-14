using System.Collections.Generic;

namespace change_tracker_explorer.Chinook;

public class MediaType : BaseEntity
{
    public MediaType()
    {
        Tracks = new HashSet<Track>();
    }

    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}