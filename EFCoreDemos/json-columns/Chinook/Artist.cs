namespace json_columns.Chinook;

public partial class Artist
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Album> Albums { get; } = new List<Album>();
}
