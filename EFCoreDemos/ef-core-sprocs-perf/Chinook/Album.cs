#nullable disable

namespace sproc_mapping.Chinook;

public sealed class Album : BaseEntity
{
    public Album()
    {
        Tracks = new HashSet<Track>();
    }

    public string Title { get; set; }
    public int ArtistId { get; set; }

    public Artist Artist { get; set; }
    public ICollection<Track> Tracks { get; set; }
}