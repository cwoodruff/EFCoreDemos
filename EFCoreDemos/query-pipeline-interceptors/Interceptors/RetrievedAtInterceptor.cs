using Microsoft.EntityFrameworkCore.Diagnostics;
using query_pipeline_interceptors.Chinook;

namespace query_pipeline_interceptors.Interceptors;

/// <summary>
/// IMaterializationInterceptor: runs for every entity instance EF creates from a query result.
/// Stamps <see cref="IHasRetrievedAt.RetrievedAt"/> (a non-mapped property) and hands each Track
/// the <see cref="IDurationFormatter"/> service it uses to compute <see cref="Track.Length"/>.
/// </summary>
public sealed class RetrievedAtInterceptor(TimeProvider clock, IDurationFormatter formatter) : IMaterializationInterceptor
{
    private int _calls;

    /// <summary>How many entity instances this interceptor has seen.</summary>
    public int Calls => Volatile.Read(ref _calls);

    public void ResetCount() => Interlocked.Exchange(ref _calls, 0);

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        // Called once per materialized entity, after its mapped properties are set.
        // Keep it cheap: a 10,000-row query calls it 10,000 times.
        Interlocked.Increment(ref _calls);

        if (entity is IHasRetrievedAt stamped)
        {
            stamped.RetrievedAt = clock.GetUtcNow();
        }

        if (entity is Track track)
        {
            track.Formatter = formatter;
        }

        return entity;
    }
}
