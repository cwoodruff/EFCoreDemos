namespace linq_query_vs_fromsql_perf.Chinook;

public class Album
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public int ArtistId { get; set; }

    public ICollection<Track> Tracks { get; set; } = new HashSet<Track>();
}
