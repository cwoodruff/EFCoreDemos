namespace shadow_properties_backing_fields.Model;

/// <summary>No AlbumId here: the foreign key is a shadow property as well.</summary>
public class Track
{
    public Track(string name, decimal unitPrice)
    {
        Name = name;
        Reprice(unitPrice);
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public decimal UnitPrice { get; private set; }

    public void Reprice(decimal unitPrice)
    {
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Price can't be negative.");
        UnitPrice = unitPrice;
    }
}
