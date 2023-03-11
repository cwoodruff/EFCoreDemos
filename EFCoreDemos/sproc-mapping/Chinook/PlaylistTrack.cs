using sproc_mapping.Chinook;

namespace sproc_mapping.Chinook;

public sealed class PlaylistTrack : BaseEntity
{
    public PlaylistTrack()
    {
        Tracks = new HashSet<Track>();
        Playlists = new HashSet<Playlist>();
    }

    public int PlaylistId { get; set; }
    public int TrackId { get; set; }

    public ICollection<Playlist> Playlists { get; set; }
    public ICollection<Track> Tracks { get; set; }
}