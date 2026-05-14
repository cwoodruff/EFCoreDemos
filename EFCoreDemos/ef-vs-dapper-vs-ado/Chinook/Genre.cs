using System.Collections.Generic;

namespace ef_vs_dapper_vs_ado.Chinook;

public class Genre : BaseEntity
{
    public Genre()
    {
        Tracks = new HashSet<Track>();
    }

    public string Name { get; set; }
    public virtual ICollection<Track> Tracks { get; set; }
}