using System.Collections.Generic;

namespace transactions_and_savechanges.Chinook;

public class MediaType : BaseEntity
{
    public MediaType()
    {
        Tracks = new HashSet<Track>();
    }

    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}