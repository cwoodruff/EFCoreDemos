#nullable disable

using System.Collections.Generic;

namespace lazy_loading.Chinook;

public class Album : BaseEntity
{
    public Album()
    {
        Tracks = new HashSet<Track>();
    }

    public string Title { get; set; }
    public int ArtistId { get; set; }

    public virtual Artist Artist { get; set; }
    public virtual ICollection<Track> Tracks { get; set; }
}