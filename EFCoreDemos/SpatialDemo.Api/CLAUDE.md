# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 9.0 ASP.NET Core Web API demonstrating spatial/geographic data handling using Entity Framework Core with SQL Server and NetTopologySuite. The application manages locations (points) and city boundaries (polygons) with geographic queries.

## Key Architecture

- **Framework**: .NET 9.0 ASP.NET Core Minimal API
- **Database**: SQL Server with spatial extensions (geography columns)
- **ORM**: Entity Framework Core 9.0 with NetTopologySuite integration
- **Spatial Data**: SRID 4326 (WGS84) coordinate system for lat/lng data
- **API Documentation**: Swagger/OpenAPI integration

### Core Components

- `Data/AppDbContext.cs`: EF Core context with spatial column configuration
- `Entities/Location.cs`: Point entity with NetTopologySuite Point geometry
- `Entities/CityBoundary.cs`: Polygon entity with NetTopologySuite Polygon geometry
- `Program.cs`: Minimal API endpoints with automatic migrations and seeding

### Spatial Features

The application demonstrates:
- Point-in-polygon queries (locations within city boundaries)
- Distance-based queries (nearby locations within radius)
- Automatic coordinate system handling (longitude=X, latitude=Y)
- Geography column types in SQL Server

## Development Commands

### Build and Run
```bash
dotnet build
dotnet run
```

### Database Operations
```bash
# Add migration
dotnet ef migrations add <MigrationName>

# Update database
dotnet ef database update

# Drop database (development)
dotnet ef database drop
```

### Development Server
- Default URLs: https://localhost:7143, http://localhost:5143
- Swagger UI: https://localhost:7143/swagger
- Root redirects to Swagger documentation

## Database Configuration

- **Default Connection**: LocalDB instance (`(localdb)\\MSSQLLocalDB`)
- **Database Name**: `EfCoreSpatialDemo`
- **Auto-migration**: Enabled in development environment
- **Seeding**: Automatic seeding of sample NYC locations and Manhattan boundary

## Key Dependencies

- `Microsoft.EntityFrameworkCore.SqlServer` - SQL Server provider
- `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` - Spatial support
- `NetTopologySuite` - Geometry library for .NET
- `Swashbuckle.AspNetCore` - API documentation

## Spatial Data Notes

- All coordinates use WGS84 (SRID 4326)
- Points stored as `geography` columns in SQL Server
- Longitude = X coordinate, Latitude = Y coordinate
- Distance queries use meters as the unit
- Polygon rings must be closed (first coordinate = last coordinate)