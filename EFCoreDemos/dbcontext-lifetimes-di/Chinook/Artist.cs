using System.Collections.Generic;

namespace dbcontext_lifetimes_di.Chinook;

public class Artist : BaseEntity
{
    public Artist()
    {
        Albums = new HashSet<Album>();
    }

    public string Name { get; set; }

    public virtual ICollection<Album> Albums { get; set; }
}