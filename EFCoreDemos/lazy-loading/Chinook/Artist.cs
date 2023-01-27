using System.Collections.Generic;

namespace lazy_loading.Chinook;

public class Artist : BaseEntity
{
    public Artist()
    {
        Albums = new HashSet<Album>();
    }

    public string Name { get; set; }

    public virtual ICollection<Album> Albums { get; set; }
}