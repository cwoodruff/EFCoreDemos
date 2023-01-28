using System.Collections.Generic;

namespace interception_db_ops.Chinook;

public sealed class Playlist : BaseEntity
{
    public string? Name { get; set; }

    public ICollection<Track>? Tracks { get; set; }
}