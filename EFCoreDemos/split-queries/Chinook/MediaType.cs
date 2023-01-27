using System.Collections.Generic;

namespace split_queries.Chinook;

public sealed class MediaType : BaseEntity
{
    public MediaType()
    {
        Tracks = new HashSet<Track>();
    }

    public string? Name { get; set; }
    
    public ICollection<Track>? Tracks { get; set; }
}