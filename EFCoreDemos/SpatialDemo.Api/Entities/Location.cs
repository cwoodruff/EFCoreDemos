using NetTopologySuite.Geometries;

namespace SpatialDemo.Api.Entities;

/// <summary>
/// A named point on Earth (SRID 4326, WGS84). X=Longitude, Y=Latitude.
/// </summary>
public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Point Coordinates { get; set; } = default!;
}