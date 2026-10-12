namespace shadow_properties_backing_fields.Model;

/// <summary>
/// A domain entity with no persistence concerns: private setters, no parameterless constructor,
/// no CreatedAt/LastUpdated, and a track list that can only change through <see cref="AddTrack"/>.
/// </summary>
public class Album
{
    // The backing field EF reads and writes (found by convention: _tracks for Tracks).
    private readonly List<Track> _tracks = [];

    // EF binds 'title' to the Title property, so no parameterless constructor is needed.
    public Album(string title)
    {
        Rename(title);
    }

    public int Id { get; private set; }

    public string Title { get; private set; } = "";

    // A read-only view: callers can't Add/Remove/Clear, not even by casting to ICollection<Track>.
    public IReadOnlyCollection<Track> Tracks => _tracks.AsReadOnly();

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("An album needs a title.", nameof(title));
        Title = title.Trim();
    }

    public Track AddTrack(string name, decimal unitPrice)
    {
        if (_tracks.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"'{Title}' already has a track called '{name}'.");

        var track = new Track(name, unitPrice);
        _tracks.Add(track);
        return track;
    }
}
