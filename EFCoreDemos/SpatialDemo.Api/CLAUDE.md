# CLAUDE.md

Guidance for Claude Code when working in this project.

## Project Overview

ASP.NET Core minimal API (.NET 10, EF Core 10.0.12) demonstrating SQL Server spatial data with NetTopologySuite: locations (points) and city boundaries (polygons) stored as `geography` columns, SRID 4326.

## Layout

- `Program.cs`: DbContext registration, startup migration + seeding, and all endpoints
- `Data/AppDbContext.cs`: EF Core context; maps `Coordinates` and `Area` to `geography`
- `Entities/Location.cs`, `Entities/CityBoundary.cs`: entities (`Point`, `Polygon`)
- `Migrations/`: single `InitialCreate` migration plus model snapshot

## Database

- SQL Server 2025 in the docker container `sql2025` at `localhost,1433` (sa login)
- Database: `spatialdemo`
- Connection string: `ConnectionStrings:DefaultConnection` in `appsettings.json` (same value hard-coded as a fallback in `Program.cs`)
- On every startup the app calls `Database.Migrate()` and seeds three NYC locations and a rectangular "New York" boundary if `Locations` is empty

## Endpoints (all in Program.cs)

- `GET /` redirects to `/swagger`
- `POST /locations`, `GET /locations`
- `GET /locations/near?longitude=&latitude=&radiusMeters=` (`IsWithinDistance`, meters)
- `POST /cities` (closes the ring if needed), `GET /cities`
- `GET /cities/{cityName}/locations` (point-in-polygon via `Contains`)

Swagger UI (Swashbuckle) is enabled in the Development environment only.

## Commands

```bash
dotnet run                                  # https://localhost:7143, http://localhost:5143
dotnet ef migrations add <Name>
dotnet ef database update
```

## Spatial Notes

- X = longitude, Y = latitude; SRID 4326
- Distances on `geography` are in meters
- Polygon rings must be closed (first coordinate = last coordinate)
