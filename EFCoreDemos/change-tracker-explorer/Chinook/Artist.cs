using System.Collections.Generic;

namespace change_tracker_explorer.Chinook;

public class Artist : BaseEntity
{
    public Artist()
    {
        Albums = new HashSet<Album>();
    }

    public string Name { get; set; }

    public virtual ICollection<Album> Albums { get; set; }
}