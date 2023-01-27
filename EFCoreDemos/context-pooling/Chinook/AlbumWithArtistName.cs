#nullable disable

namespace context_pooling.Chinook;

public abstract class AlbumWithArtistName : BaseEntity
{
    public string Title { get; set; }
    public int ArtistId { get; set; }
    public string Name { get; set; }
}