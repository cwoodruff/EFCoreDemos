using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using value_converters;
using value_converters.Model;

// A throwaway SQLite file next to the binaries, rebuilt on every run.
var connectionString = $"Data Source={Path.Combine(AppContext.BaseDirectory, "catalog.db")};Pooling=False";

// SQL logging is switched on only where the executed command is the point.
var showSql = false;
using var db = new CatalogContext(CatalogContext.CreateOptions(connectionString, message =>
{
    if (showSql) Console.WriteLine(message);
}));

db.Database.EnsureDeleted();
db.Database.EnsureCreated();
CatalogSeed.Seed(db);
db.ChangeTracker.Clear();

// ----------------------------------------------------------------------------------------------
Header("1. What the converters put in the database");
Console.WriteLine(db.Database.GenerateCreateScript().Trim());
Console.WriteLine();
Console.WriteLine("Raw Tracks rows (plain SQL, no EF):");
PrintRaw("SELECT \"Id\", \"Name\", \"Genre\", \"AlbumId\", \"Tags\" FROM \"Tracks\" ORDER BY \"Id\" LIMIT 3");
Console.WriteLine("Raw Customers rows:");
PrintRaw("SELECT \"Id\", \"Name\", \"Email\" FROM \"Customers\" ORDER BY \"Id\"");
Console.WriteLine();
Console.WriteLine("Genre is the enum NAME (TEXT), ids are plain INTEGERs, Tags is JSON text, Email is ciphertext.");
Console.WriteLine("Your C# never sees any of that: it gets Genre.Rock, TrackId(1), List<string> and plain e-mails.");

// ----------------------------------------------------------------------------------------------
Header("2. Enum stored as string: HasConversion<string>()");
ShowQuery(ConverterQueries.TracksInGenre(db, Genre.Jazz), t => t.Name);
Console.WriteLine("The parameter is converted too (Genre.Jazz -> 'Jazz'), so equality just works - and the column is readable.");

// ----------------------------------------------------------------------------------------------
Header("3. Strongly typed ids, registered once in ConfigureConventions");
Console.WriteLine("""
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<TrackId>().HaveConversion<TrackIdConverter>();
        configurationBuilder.Properties<AlbumId>().HaveConversion<AlbumIdConverter>();
    }
    """);
var trackType = db.Model.FindEntityType(typeof(Track))!;
foreach (var name in new[] { nameof(Track.Id), nameof(Track.AlbumId) })
{
    var property = trackType.FindProperty(name)!;
    Console.WriteLine($"  Track.{name,-8} {property.ClrType.Name,-8} -> converter {property.GetValueConverter()?.GetType().Name}, stored as {property.GetValueConverter()?.ProviderClrType.Name}");
}

Console.WriteLine();
ShowQuery(ConverterQueries.TrackById(db, new TrackId(3)), t => t.Name);
var album = db.Albums.Include(a => a.Tracks).Single(a => a.Id == new AlbumId(2));
Console.WriteLine($"Include through the converted foreign key: {album.Title} -> {string.Join(", ", album.Tracks.Select(t => t.Name))}");
Console.WriteLine("db.Tracks.Where(t => t.Id == new AlbumId(2)) doesn't compile - that's the whole point of typed ids.");

// ----------------------------------------------------------------------------------------------
Header("4. Custom ValueConverters: an encrypted column and a JSON list");
Console.WriteLine("DEMO ONLY: EncryptedStringConverter uses AES with a fixed key and IV hard-coded in source.");
Console.WriteLine("A fixed IV makes it deterministic (same e-mail -> same ciphertext), which is what makes equality work.");
Console.WriteLine();
ShowQuery(ConverterQueries.CustomerByEmail(db, "ada@example.com"), c => c.Name);
Console.WriteLine("The parameter went through the converter: EF compared ciphertext with ciphertext.");
Console.WriteLine();

var hellsBells = db.Tracks.Single(t => t.Id == new TrackId(1));
hellsBells.Tags.Add("classic");
Console.WriteLine($"Tags after Add(\"classic\"): [{string.Join(", ", hellsBells.Tags)}] - mutated in place, not reassigned.");
showSql = true;
db.SaveChanges();
showSql = false;
Console.WriteLine("The UPDATE happened because Tags has a ValueComparer that compares contents. Without one,");
Console.WriteLine("EF compares the list by reference, sees the same List instance, and silently saves nothing.");
db.ChangeTracker.Clear();

// ----------------------------------------------------------------------------------------------
Header("5. The trap: filtering and sorting on converted values");
Console.WriteLine("EF translates queries against the STORED value. Whatever the converter produces is what SQL compares.");

var allTracks = db.Tracks.AsNoTracking().ToList();
var allCustomers = db.Customers.AsNoTracking().ToList();

Trap("a) OrderBy(t => t.Genre) - enum stored as string",
    ConverterQueries.TracksOrderedByGenre(db), t => $"{t.Genre}",
    allTracks.OrderBy(t => t.Genre).ThenBy(t => t.Id.Value).Select(t => $"{t.Genre}"),
    "SQL sorts the text alphabetically; C# sorts by the enum's numeric value.");

Trap("b) Where(t => t.Genre > Genre.Jazz)",
    ConverterQueries.TracksAfterGenre(db, Genre.Jazz), t => $"{t.Genre}",
    allTracks.Where(t => t.Genre > Genre.Jazz).Select(t => $"{t.Genre}"),
    "Wrong rows, no error: 'Rock' > 'Jazz' as text, but Rock (0) < Jazz (1) as an enum.");

Trap("c) Where(c => c.Email.StartsWith(\"a\")) - encrypted column, constant prefix",
    ConverterQueries.CustomersWhoseEmailStartsWithA(db), c => c.Name,
    allCustomers.Where(c => c.Email.StartsWith("a")).Select(c => c.Name),
    "EF built the pattern 'a%' and encrypted it, wildcard and all. Nothing can match. No error.");

var prefix = "a";
TrapThrows("d) Where(c => c.Email.StartsWith(prefix)) - encrypted column, prefix from a variable",
    ConverterQueries.CustomersWhoseEmailStartsWith(db, prefix),
    "With a parameter EF adds ESCAPE '\\' to the LIKE - and the escape character went through the\n   converter too. Same LINQ shape as (c), different failure: this time it blows up.");

Trap("e) Where(c => c.Email.Contains(\"example.org\")) - encrypted column",
    ConverterQueries.CustomersWhoseEmailContains(db, "example.org"), c => c.Name,
    allCustomers.Where(c => c.Email.Contains("example.org")).Select(c => c.Name),
    "instr() looks for an encrypted fragment inside the ciphertext. It's never there. No error.");

Trap("f) OrderBy(c => c.Email) - encrypted column",
    ConverterQueries.CustomersOrderedByEmail(db), c => c.Name,
    allCustomers.OrderBy(c => c.Email).ThenBy(c => c.Id).Select(c => c.Name),
    "Sorted by ciphertext: stable, but meaningless.");

TrapThrows("g) Where(t => t.Id.Value > 5) - reaching inside a typed id",
    ConverterQueries.TracksWithIdAbove(db, 5),
    "SQL only knows the INTEGER column; it has no idea what TrackId.Value is.");

TrapThrows("h) Where(t => t.Tags.Contains(\"single\")) - list behind a custom converter",
    ConverterQueries.TracksTagged(db, "single"),
    "To EF the column is an opaque string produced by your converter; it can't look inside.");

Console.WriteLine("   The fix for (h): drop the converter and let EF map List<string> as a primitive collection.");
Console.WriteLine("   Moods is exactly that - stored as JSON too, but queryable:");
ShowQuery(ConverterQueries.TracksInMood(db, "calm"), t => t.Name, indent: "   ");

// ----------------------------------------------------------------------------------------------
Header("6. Rules of thumb");
Console.WriteLine("""
    - Equality on a converted value is safe: EF converts the constant/parameter and compares stored values.
    - Ordering and <, > compare the STORED value. Enum-as-string sorts alphabetically; keep enums as
      numbers (the default) if you sort or range-filter on them, or add a separate sort column.
    - String functions (StartsWith, Contains, Like) on an encrypted or otherwise transformed column run
      against the transformed text - and the pattern goes through the converter too. Usually wrong results
      with no exception, sometimes a database error. Only deterministic equality survives.
    - Members of a converted type (TrackId.Value) and collection operators on a converted list can't be
      translated: compare whole values, or use a primitive collection / complex type / JSON column instead.
    - Mutable types behind a converter (lists, arrays) need a ValueComparer or edits aren't saved.
    - Register converters for your own types once with ConfigureConventions, not per property.
    """);

return;

// ----------------------------------------------------------------------------------------------

void ShowQuery<T>(IQueryable<T> query, Func<T, string> show, string indent = "") where T : class
{
    foreach (var line in query.ToQueryString().Split('\n'))
        Console.WriteLine(indent + line.TrimEnd());
    Console.WriteLine($"{indent}-> {string.Join(", ", query.AsNoTracking().AsEnumerable().Select(show))}");
}

void Trap<T>(string title, IQueryable<T> query, Func<T, string> show, IEnumerable<string> expected, string why) where T : class
{
    Console.WriteLine();
    Console.WriteLine(title);
    var sql = query.ToQueryString().Split('\n').Select(l => l.TrimEnd()).Last();
    var actual = query.AsNoTracking().AsEnumerable().Select(show).ToList();
    var wanted = expected.ToList();
    Console.WriteLine($"   SQL:        {sql.Trim()}");
    Console.WriteLine($"   Database:   [{string.Join(", ", actual)}]");
    Console.WriteLine($"   C# meaning: [{string.Join(", ", wanted)}]");
    Console.WriteLine(actual.SequenceEqual(wanted) ? "   (same rows)" : "   DIFFERENT - " + why);
}

void TrapThrows<T>(string title, IQueryable<T> query, string why) where T : class
{
    Console.WriteLine();
    Console.WriteLine(title);
    try
    {
        // ToQueryString succeeds whenever translation does, so it's printed before executing.
        Console.WriteLine($"   SQL:        {query.ToQueryString().Split('\n').Select(l => l.Trim()).Last()}");
        query.AsNoTracking().ToList();
        Console.WriteLine("   Ran without an error (unexpected).");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("could not be translated"))
    {
        var message = ex.Message;
        var start = message.IndexOf("could not be translated", StringComparison.Ordinal);
        var extra = message.IndexOf("Additional information:", StringComparison.Ordinal);
        var detail = extra >= 0 ? " " + message[extra..].Split(". ")[0] + "." : "";
        Console.WriteLine($"   InvalidOperationException: the LINQ expression {message[start..].Split(". ")[0]}.{detail}");
        Console.WriteLine($"   {why}");
    }
    catch (SqliteException ex)
    {
        Console.WriteLine($"   SqliteException: {ex.Message}");
        Console.WriteLine($"   {why}");
    }
}

void PrintRaw(string sql)
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        var values = Enumerable.Range(0, reader.FieldCount).Select(i => $"{reader.GetName(i)}={reader.GetValue(i)}");
        Console.WriteLine("  " + string.Join("  ", values));
    }
}

static void Header(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 90));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 90));
}
