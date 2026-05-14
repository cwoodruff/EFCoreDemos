using System.Collections.Generic;

namespace n_plus_one_problem.Chinook;

public class Playlist : BaseEntity
{
    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}