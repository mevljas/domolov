using Domolov.Application.Abstractions;

namespace Domolov.Application.Common;

/// <summary>Optimistic concurrency with PostgreSQL xmin tokens.</summary>
public static class Concurrency
{
    /// <summary>
    /// Throws when the caller's expected version is stale, and pins the original value so a
    /// concurrent write between load and save also fails.
    /// </summary>
    public static void Check<TEntity>(
        IAppDbContext db,
        TEntity entity,
        Func<TEntity, uint> version,
        uint? expected
    )
        where TEntity : class
    {
        if (expected is not uint e)
        {
            return;
        }

        if (version(entity) != e)
        {
            throw new PreconditionFailedException();
        }

        db.Entry(entity).Property("Version").OriginalValue = e;
    }
}
