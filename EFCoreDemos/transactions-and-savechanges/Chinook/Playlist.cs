using System.Collections.Generic;

namespace transactions_and_savechanges.Chinook;

public class Playlist : BaseEntity
{
    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}