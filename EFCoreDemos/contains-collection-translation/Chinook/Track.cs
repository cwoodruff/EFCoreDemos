namespace contains_collection_translation.Chinook;

public class Track
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int? AlbumId { get; set; }
    public int MediaTypeId { get; set; }
    public int? GenreId { get; set; }
    public string? Composer { get; set; }
    public int Milliseconds { get; set; }
    public int? Bytes { get; set; }
    public decimal UnitPrice { get; set; }
}
