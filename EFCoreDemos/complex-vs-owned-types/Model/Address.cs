namespace complex_vs_owned_types.Model;

// The value object both models use. A record, so two addresses with the same
// street/city/postcode/country are equal - which is exactly what a value object means.
// Complex types embrace that; owned types ignore it and track each instance by a hidden key.
public record Address
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string PostCode { get; init; }
    public required string Country { get; init; }

    public override string ToString() => $"{Street}, {City} {PostCode}, {Country}";
}

// EF 10: complex types can be structs.
public readonly record struct GeoPoint(double Latitude, double Longitude);
