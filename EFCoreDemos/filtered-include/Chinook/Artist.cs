using System.Collections.Generic;

namespace filtered_include.Chinook;

public sealed class Artist : BaseEntity
{
    public Artist()
    {
        Albums = new HashSet<Album>();
    }

    public string Name { get; set; }

    public ICollection<Album> Albums { get; set; }
}