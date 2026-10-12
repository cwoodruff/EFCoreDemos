using value_converters.Model;

namespace value_converters;

/// <summary>
/// Queries against converted properties. The first group translates the way you'd hope; the second group
/// is the trap: EF compares the *stored* (converted) values, or can't translate the query at all.
/// </summary>
public static class ConverterQueries
{
    // ---- Translates fine ------------------------------------------------------------------------

    /// <summary>Equality on an enum stored as string: the constant is converted too -> WHERE Genre = 'Jazz'.</summary>
    public static IQueryable<Track> TracksInGenre(CatalogContext db, Genre genre) =>
        db.Tracks.Where(t => t.Genre == genre);

    /// <summary>Equality on a strongly typed id: WHERE Id = 3.</summary>
    public static IQueryable<Track> TrackById(CatalogContext db, TrackId id) =>
        db.Tracks.Where(t => t.Id == id);

    /// <summary>Equality on an encrypted column works only because the encryption is deterministic.</summary>
    public static IQueryable<Customer> CustomerByEmail(CatalogContext db, string email) =>
        db.Customers.Where(c => c.Email == email);

    /// <summary>A primitive collection (no converter): EF knows it's a JSON array and uses json_each.</summary>
    public static IQueryable<Track> TracksInMood(CatalogContext db, string mood) =>
        db.Tracks.Where(t => t.Moods.Contains(mood));

    // ---- The trap ---------------------------------------------------------------------------------

    /// <summary>Sorts by the stored text: alphabetical, not the enum's declared order.</summary>
    public static IQueryable<Track> TracksOrderedByGenre(CatalogContext db) =>
        db.Tracks.OrderBy(t => t.Genre).ThenBy(t => t.Id);

    /// <summary>Compares the stored text: Genre > 'Jazz' as strings, not as enum values.</summary>
    public static IQueryable<Track> TracksAfterGenre(CatalogContext db, Genre genre) =>
        db.Tracks.Where(t => t.Genre > genre);

    /// <summary>
    /// A constant prefix: EF builds the LIKE pattern 'a%' and runs it through the converter. The encrypted
    /// pattern has no wildcard left and matches nothing - silently.
    /// </summary>
    public static IQueryable<Customer> CustomersWhoseEmailStartsWithA(CatalogContext db) =>
        db.Customers.Where(c => c.Email.StartsWith("a"));

    /// <summary>
    /// A variable prefix (the realistic search-box case): EF adds ESCAPE '\' to the LIKE, and that escape
    /// character goes through the converter too. SQLite rejects the 24-character "escape" and the query throws.
    /// </summary>
    public static IQueryable<Customer> CustomersWhoseEmailStartsWith(CatalogContext db, string prefix) =>
        db.Customers.Where(c => c.Email.StartsWith(prefix));

    /// <summary>Same story with Contains: searching ciphertext for an encrypted fragment.</summary>
    public static IQueryable<Customer> CustomersWhoseEmailContains(CatalogContext db, string text) =>
        db.Customers.Where(c => c.Email.Contains(text));

    /// <summary>Sorts by ciphertext: a stable but meaningless order.</summary>
    public static IQueryable<Customer> CustomersOrderedByEmail(CatalogContext db) =>
        db.Customers.OrderBy(c => c.Email).ThenBy(c => c.Id);

    /// <summary>Reaching inside a converted value: EF can't see TrackId.Value in SQL -> throws.</summary>
    public static IQueryable<Track> TracksWithIdAbove(CatalogContext db, int value) =>
        db.Tracks.Where(t => t.Id.Value > value);

    /// <summary>A list behind a custom converter is opaque text to EF: Contains can't be translated -> throws.</summary>
    public static IQueryable<Track> TracksTagged(CatalogContext db, string tag) =>
        db.Tracks.Where(t => t.Tags.Contains(tag));
}
