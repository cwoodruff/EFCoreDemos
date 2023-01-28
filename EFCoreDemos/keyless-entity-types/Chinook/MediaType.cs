using System.Collections.Generic;

namespace keylessentitytypes.Chinook;

public sealed class MediaType : BaseEntity
{
    public MediaType()
    {
        Tracks = new HashSet<Track>();
    }

    public string? Name { get; set; }
    
    public ICollection<Track>? Tracks { get; set; }
}