using System.Collections.Generic;

namespace simple_logging_improved_diagnostics.Chinook;

public sealed class Artist : BaseEntity
{
    public Artist()
    {
        Albums = new HashSet<Album>();
    }

    public string? Name { get; set; }

    public ICollection<Album> Albums { get; set; }
}