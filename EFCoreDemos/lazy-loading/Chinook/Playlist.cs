using System.Collections.Generic;

namespace lazy_loading.Chinook;

public class Playlist : BaseEntity
{
    public string? Name { get; set; }

    public virtual ICollection<Track>? Tracks { get; set; }
}