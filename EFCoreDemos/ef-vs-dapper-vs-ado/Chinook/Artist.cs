using System.Collections.Generic;

namespace ef_vs_dapper_vs_ado.Chinook;

public class Artist : BaseEntity
{
    public Artist()
    {
        Albums = new HashSet<Album>();
    }

    public string Name { get; set; }

    public virtual ICollection<Album> Albums { get; set; }
}