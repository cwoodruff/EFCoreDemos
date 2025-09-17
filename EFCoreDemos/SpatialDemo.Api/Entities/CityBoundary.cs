using NetTopologySuite.Geometries;

namespace SpatialDemo.Api.Entities;

/// <summary>
/// A named polygonal area (SRID 4326, WGS84).
/// </summary>
public class CityBoundary
{
    public int Id { get; set; }
    public string CityName { get; set; } = string.Empty;
    public Polygon Area { get; set; } = default!;
}