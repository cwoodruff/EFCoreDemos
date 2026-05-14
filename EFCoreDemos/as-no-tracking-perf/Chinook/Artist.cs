using System.Collections.Generic;

namespace as_no_tracking_perf.Chinook;

public class Artist : BaseEntity
{
    public Artist()
    {
        Albums = new HashSet<Album>();
    }

    public string Name { get; set; }

    public virtual ICollection<Album> Albums { get; set; }
}