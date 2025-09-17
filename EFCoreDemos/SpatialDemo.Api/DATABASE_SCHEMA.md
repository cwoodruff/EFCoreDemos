# Database Schema Documentation

## Overview

This document describes the database schema for the SpatialDemo.Api project, which uses SQL Server with spatial extensions and Entity Framework Core 9.0 with NetTopologySuite integration.

## Database Configuration

- **Database Provider**: SQL Server
- **Spatial Support**: NetTopologySuite with SQL Server geography columns
- **SRID**: 4326 (WGS84 coordinate system)
- **Default Database Name**: `EfCoreSpatialDemo`
- **Default Connection**: LocalDB instance `(localdb)\\MSSQLLocalDB`

## Tables

### Locations

Stores named geographic points (latitude/longitude coordinates).

```sql
CREATE TABLE [Locations] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Coordinates] geography NOT NULL,
    CONSTRAINT [PK_Locations] PRIMARY KEY ([Id])
);
```

**Columns:**
- `Id` (int, IDENTITY): Primary key, auto-incrementing
- `Name` (nvarchar(200), NOT NULL): Human-readable name for the location
- `Coordinates` (geography, NOT NULL): Point geometry storing longitude/latitude

**Entity Mapping:**
- Maps to `SpatialDemo.Api.Entities.Location`
- `Coordinates` property is `NetTopologySuite.Geometries.Point`

### CityBoundaries

Stores named polygonal areas representing city boundaries.

```sql
CREATE TABLE [CityBoundaries] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [CityName] nvarchar(200) NOT NULL,
    [Area] geography NOT NULL,
    CONSTRAINT [PK_CityBoundaries] PRIMARY KEY ([Id])
);
```

**Columns:**
- `Id` (int, IDENTITY): Primary key, auto-incrementing
- `CityName` (nvarchar(200), NOT NULL): Name of the city or area
- `Area` (geography, NOT NULL): Polygon geometry defining the boundary

**Entity Mapping:**
- Maps to `SpatialDemo.Api.Entities.CityBoundary`
- `Area` property is `NetTopologySuite.Geometries.Polygon`

## Spatial Data Specifications

### Coordinate System
- **SRID**: 4326 (World Geodetic System 1984)
- **Format**: Geographic coordinates (latitude/longitude)
- **Units**: Degrees for coordinates, meters for distance calculations
- **Axis Order**: X = Longitude, Y = Latitude

### Geography Column Requirements

#### Points (Locations.Coordinates)
- Must be valid Point geometries
- Longitude range: -180 to 180 degrees
- Latitude range: -90 to 90 degrees
- SRID must be 4326

#### Polygons (CityBoundaries.Area)
- Must be valid Polygon geometries with closed LinearRing
- First and last coordinates must be identical (ring closure)
- No self-intersections allowed
- SRID must be 4326
- Coordinates in longitude/latitude order

### Sample Data

The application seeds the following sample data on startup:

**Locations:**
```sql
INSERT INTO [Locations] ([Name], [Coordinates]) VALUES
('Times Square', geography::Point(40.7580, -73.9851, 4326)),
('Central Park', geography::Point(40.7829, -73.9654, 4326)),
('Empire State Building', geography::Point(40.7484, -73.9857, 4326));
```

**CityBoundaries:**
```sql
-- Manhattan boundary (simplified rectangle for demo)
INSERT INTO [CityBoundaries] ([CityName], [Area]) VALUES
('New York', geography::STPolyFromText('POLYGON((-74.02 40.70, -73.93 40.70, -73.93 40.83, -74.02 40.83, -74.02 40.70))', 4326));
```

## Spatial Queries Supported

### Distance-Based Queries
```sql
-- Find locations within radius (meters)
SELECT * FROM Locations
WHERE Coordinates.STDistance(geography::Point(@lat, @lng, 4326)) <= @radiusMeters;
```

### Point-in-Polygon Queries
```sql
-- Find locations within a city boundary
SELECT l.* FROM Locations l
JOIN CityBoundaries c ON c.Area.STContains(l.Coordinates) = 1
WHERE c.CityName = @cityName;
```

## Migration Notes

- EF Core migrations will automatically create the required spatial indexes
- SQL Server spatial extensions must be available
- NetTopologySuite provider handles SRID configuration automatically
- Geography columns use SQL Server's built-in spatial types

## Performance Considerations

- Spatial indexes are automatically created by EF Core for geography columns
- Consider adding explicit spatial indexes for complex queries:
  ```sql
  CREATE SPATIAL INDEX [IX_Locations_Coordinates] ON [Locations] ([Coordinates]);
  CREATE SPATIAL INDEX [IX_CityBoundaries_Area] ON [CityBoundaries] ([Area]);
  ```

## Constraints and Validations

- All spatial data must use SRID 4326
- Polygon rings must be properly closed
- Points must have valid latitude/longitude values
- Entity validation occurs at the application level through NetTopologySuite