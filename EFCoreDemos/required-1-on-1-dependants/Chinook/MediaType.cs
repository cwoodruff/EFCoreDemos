using System.Collections.Generic;

namespace required_1_on_1_dependants.Chinook;

public sealed class MediaType : BaseEntity
{
    public MediaType()
    {
        Tracks = new HashSet<Track>();
    }

    public string? Name { get; set; }

    public ICollection<Track>? Tracks { get; set; }
}