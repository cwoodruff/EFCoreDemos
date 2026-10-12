using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace value_converters.Model;

// Strongly typed IDs: a TrackId can't be passed where an AlbumId is expected - the compiler says no.
public readonly record struct TrackId(int Value);

public readonly record struct AlbumId(int Value);

// Registered once for the whole model in CatalogContext.ConfigureConventions, so every TrackId / AlbumId
// property (keys and foreign keys alike) is stored as a plain INTEGER column.
public sealed class TrackIdConverter() : ValueConverter<TrackId, int>(id => id.Value, value => new TrackId(value));

public sealed class AlbumIdConverter() : ValueConverter<AlbumId, int>(id => id.Value, value => new AlbumId(value));
