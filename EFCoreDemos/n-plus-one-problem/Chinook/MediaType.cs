using System.Collections.Generic;

namespace n_plus_one_problem.Chinook;

public class MediaType : BaseEntity
{
    public MediaType()
    {
        Tracks = new HashSet<Track>();
    }

    public string Name { get; set; }

    public virtual ICollection<Track> Tracks { get; set; }
}