namespace Domolov.Domain.Common;

/// <summary>Identifier generation for aggregates.</summary>
public static class Ids
{
    /// <summary>Time-ordered UUID v7 so new rows land at the end of B-tree indexes.</summary>
    public static Guid New() => Guid.CreateVersion7();
}
