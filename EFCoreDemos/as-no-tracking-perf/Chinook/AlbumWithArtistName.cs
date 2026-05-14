#nullable disable

namespace as_no_tracking_perf.Chinook;

public abstract class AlbumWithArtistName : BaseEntity
{
    public string Title { get; set; }
    public int ArtistId { get; set; }
    public string Name { get; set; }
}