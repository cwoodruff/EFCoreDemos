using System.Collections.Generic;

namespace ef_vs_dapper_vs_ado.Chinook;

public class Playlist : BaseEntity
{
    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}