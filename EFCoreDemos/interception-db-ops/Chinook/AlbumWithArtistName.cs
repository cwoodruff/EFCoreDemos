#nullable disable

namespace interception_db_ops.Chinook;

public abstract class AlbumWithArtistName : BaseEntity
{
    public string Title { get; set; }
    public int ArtistId { get; set; }
    public string Name { get; set; }
}