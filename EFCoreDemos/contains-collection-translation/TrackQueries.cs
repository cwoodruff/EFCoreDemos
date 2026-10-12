using contains_collection_translation.Chinook;
using Microsoft.EntityFrameworkCore;

namespace contains_collection_translation;

/// <summary>How a query asks EF to translate <c>ids.Contains(t.Id)</c>.</summary>
public enum Strategy
{
    /// <summary>Plain <c>ids.Contains(t.Id)</c>: whatever the context's default mode is (EF 10: MultipleParameters).</summary>
    Default,

    /// <summary><c>EF.Constant(ids).Contains(t.Id)</c>: the values are inlined into the SQL text.</summary>
    Constant,

    /// <summary><c>EF.Parameter(ids).Contains(t.Id)</c>: one JSON parameter, unpacked server-side with OPENJSON.</summary>
    Parameter,
}

public static class TrackQueries
{
    /// <summary>Prefix of every query's TagWith comment, so its plans can be found in the plan cache.</summary>
    public const string TagPrefix = "contains-demo";

    public static string Tag(string run, Strategy strategy) => $"{TagPrefix} {run} {strategy}";

    /// <summary>
    /// Tracks whose Id is in <paramref name="ids"/>. The three branches differ only in how the
    /// collection is wrapped; the tag makes each strategy's plans identifiable in sys.dm_exec_sql_text.
    /// </summary>
    public static IQueryable<Track> ByIds(ChinookContext context, int[] ids, Strategy strategy, string run)
    {
        var tracks = context.Tracks.AsNoTracking().TagWith(Tag(run, strategy));

        return strategy switch
        {
            Strategy.Default => tracks.Where(t => ids.Contains(t.Id)),
            Strategy.Constant => tracks.Where(t => EF.Constant(ids).Contains(t.Id)),
            Strategy.Parameter => tracks.Where(t => EF.Parameter(ids).Contains(t.Id)),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
        };
    }

    /// <summary><paramref name="count"/> distinct, sorted track ids drawn from Chinook's 1..3503.</summary>
    public static int[] RandomIds(Random random, int count) =>
        Enumerable.Range(1, 3503).OrderBy(_ => random.Next()).Take(count).Order().ToArray();
}
