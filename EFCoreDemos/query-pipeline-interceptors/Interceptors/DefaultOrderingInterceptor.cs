using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace query_pipeline_interceptors.Interceptors;

/// <summary>
/// IQueryExpressionInterceptor: sees the LINQ expression tree before EF translates it and may return a
/// different one. This one adds <c>OrderBy(e =&gt; e.Key)</c> to any top-level entity query that has no
/// ordering, so Skip/Take paging and First() are deterministic.
///
/// Rules:
/// - Queries that already contain OrderBy/OrderByDescending are left alone.
/// - Aggregates (Count, Any, Sum, ...) are left alone: order doesn't change the answer.
/// - The ordering is inserted at the highest point where the element type is still an entity (so before a
///   Skip/Take or a Select to a DTO). If the query projects before that, it is left alone.
/// </summary>
public sealed class DefaultOrderingInterceptor : IQueryExpressionInterceptor
{
    private static readonly HashSet<string> OrderingMethods =
        [nameof(Queryable.OrderBy), nameof(Queryable.OrderByDescending), nameof(Queryable.Order), nameof(Queryable.OrderDescending)];

    // Element-type-preserving operators the ordering may be placed above.
    private static readonly HashSet<string> PassThroughMethods =
    [
        nameof(Queryable.Where), nameof(Queryable.Distinct),
        nameof(EntityFrameworkQueryableExtensions.Include), nameof(EntityFrameworkQueryableExtensions.ThenInclude),
        nameof(EntityFrameworkQueryableExtensions.AsNoTracking), nameof(EntityFrameworkQueryableExtensions.AsTracking),
        nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution),
        nameof(RelationalQueryableExtensions.AsSplitQuery), nameof(RelationalQueryableExtensions.AsSingleQuery),
        nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters), nameof(EntityFrameworkQueryableExtensions.IgnoreAutoIncludes),
        nameof(EntityFrameworkQueryableExtensions.TagWith), nameof(EntityFrameworkQueryableExtensions.TagWithCallSite)
    ];

    // Terminal operators whose result depends on order: the ordering goes on their source.
    private static readonly HashSet<string> OrderSensitiveTerminals =
    [
        nameof(Queryable.First), nameof(Queryable.FirstOrDefault),
        nameof(Queryable.Last), nameof(Queryable.LastOrDefault),
        nameof(Queryable.ElementAt), nameof(Queryable.ElementAtOrDefault)
    ];

    private static readonly MethodInfo OrderByMethod = QueryableMethod(nameof(Queryable.OrderBy));
    private static readonly MethodInfo ThenByMethod = QueryableMethod(nameof(Queryable.ThenBy));

    private int _calls;

    /// <summary>How many times EF has asked this interceptor to look at a query (= query compilations).</summary>
    public int Calls => Volatile.Read(ref _calls);

    public void ResetCount() => Interlocked.Exchange(ref _calls, 0);

    /// <summary>The last expression received and returned - for printing in the demo.</summary>
    public (Expression Before, Expression After, bool Rewritten)? LastQuery { get; private set; }

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
    {
        Interlocked.Increment(ref _calls);

        var model = eventData.Context?.Model;
        var rewritten = model is null ? queryExpression : AddDefaultOrdering(queryExpression, model);

        LastQuery = (queryExpression, rewritten, !ReferenceEquals(rewritten, queryExpression));
        return rewritten;
    }

    /// <summary>Returns <paramref name="query"/> with a key ordering added, or the same instance if none is needed.</summary>
    public static Expression AddDefaultOrdering(Expression query, IModel model)
    {
        // A terminal operator (First, Count, ...) wraps the sequence; only order-sensitive ones get an ordering.
        if (query is MethodCallExpression terminal && !IsQueryable(terminal.Type) && IsLinqOperator(terminal.Method))
        {
            if (!OrderSensitiveTerminals.Contains(terminal.Method.Name))
            {
                return query;
            }

            var source = terminal.Arguments[0];
            var orderedSource = OrderSequence(source, model);
            return ReferenceEquals(orderedSource, source)
                ? query
                : terminal.Update(terminal.Object, [orderedSource, .. terminal.Arguments.Skip(1)]);
        }

        return IsQueryable(query.Type) ? OrderSequence(query, model) : query;
    }

    private static Expression OrderSequence(Expression sequence, IModel model)
    {
        // Unwind the operator chain: chain[0] is the outermost call, the last item the query root.
        var chain = new List<MethodCallExpression>();
        var current = sequence;
        while (current is MethodCallExpression call && IsLinqOperator(call.Method) && call.Arguments.Count > 0)
        {
            if (OrderingMethods.Contains(call.Method.Name))
            {
                return sequence; // already ordered somewhere - respect the author's choice
            }

            chain.Add(call);
            current = call.Arguments[0];
        }

        var root = current;
        var entityType = FindEntityType(root.Type, model);
        if (entityType?.FindPrimaryKey() is not { } key || key.Properties.Any(p => p.PropertyInfo is null))
        {
            return sequence; // not an entity query, or a key we can't express as member access
        }

        // Walk up from the root while the element type stays the entity and the operator is pass-through.
        // The ordering goes just above the last such operator (i.e. below the first Skip/Take/Select/...).
        var insertAt = chain.Count; // index in chain of the first call that will sit ABOVE the ordering
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            if (!PassThroughMethods.Contains(chain[i].Method.Name) || ElementType(chain[i].Type) != entityType.ClrType)
            {
                break;
            }

            insertAt = i;
        }

        // ThenInclude needs an IIncludableQueryable source, so never put the ordering directly beneath one.
        while (insertAt > 0 && insertAt < chain.Count && chain[insertAt - 1].Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude))
        {
            insertAt--;
        }

        var below = insertAt == chain.Count ? root : chain[insertAt];
        var ordered = ApplyKeyOrdering(below, entityType.ClrType, key);

        // Rebuild every operator above the insertion point on top of the ordered sequence.
        Expression rebuilt = ordered;
        for (var i = insertAt - 1; i >= 0; i--)
        {
            rebuilt = chain[i].Update(chain[i].Object, [rebuilt, .. chain[i].Arguments.Skip(1)]);
        }

        return rebuilt;
    }

    private static Expression ApplyKeyOrdering(Expression source, Type entityClrType, IKey key)
    {
        Expression result = source;
        var first = true;
        foreach (var property in key.Properties)
        {
            var parameter = Expression.Parameter(entityClrType, "e");
            var selector = Expression.Lambda(Expression.Property(parameter, property.PropertyInfo!), parameter);
            var method = (first ? OrderByMethod : ThenByMethod).MakeGenericMethod(entityClrType, property.ClrType);
            result = Expression.Call(method, result, Expression.Quote(selector));
            first = false;
        }

        return result;
    }

    private static IEntityType? FindEntityType(Type sequenceType, IModel model) =>
        ElementType(sequenceType) is { } element ? model.FindEntityType(element) : null;

    private static Type? ElementType(Type sequenceType) =>
        sequenceType.IsGenericType && typeof(IQueryable).IsAssignableFrom(sequenceType)
            ? sequenceType.GetInterfaces().Append(sequenceType)
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>))
                ?.GetGenericArguments()[0]
            : null;

    private static bool IsQueryable(Type type) => typeof(IQueryable).IsAssignableFrom(type);

    private static bool IsLinqOperator(MethodInfo method) =>
        method.DeclaringType == typeof(Queryable)
        || method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
        || method.DeclaringType == typeof(RelationalQueryableExtensions);

    private static MethodInfo QueryableMethod(string name) =>
        typeof(Queryable).GetMethods().Single(m =>
            m.Name == name && m.GetParameters().Length == 2
            && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2);
}
