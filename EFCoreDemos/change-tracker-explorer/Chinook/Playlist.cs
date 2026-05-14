using System.Collections.Generic;

namespace change_tracker_explorer.Chinook;

public class Playlist : BaseEntity
{
    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}