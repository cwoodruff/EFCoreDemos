namespace Chinook.Domain.Entities;

public sealed class Genre : BaseEntity
{
    public Genre()
    {
        Tracks = new HashSet<Track>();
    }

    public string? Name { get; set; }
    public ICollection<Track>? Tracks { get; set; }
}