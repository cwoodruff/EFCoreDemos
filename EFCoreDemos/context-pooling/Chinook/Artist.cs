namespace context_pooling.Chinook;

public partial class Artist
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Album> Albums { get; } = new List<Album>();
}
