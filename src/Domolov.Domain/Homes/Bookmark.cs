using Domolov.Domain.Common;

namespace Domolov.Domain.Homes;

/// <summary>Where the operator is with a Bookmarked Home. Ordered from least to most advanced.</summary>
public enum BookmarkStage
{
    Interested = 0,
    Contacted = 1,
    ViewingScheduled = 2,
    Viewed = 3,
    OfferMade = 4,
    Rejected = 5,
}

/// <summary>The operator's tracking record for a Home, with a stage and a private note.</summary>
public sealed class Bookmark
{
    public const int NoteMaxLength = 4000;

    private Bookmark() { }

    public Bookmark(Guid homeId, BookmarkStage stage, string? note, DateTimeOffset now)
    {
        HomeId = homeId;
        Stage = stage;
        Note = NormalizeNote(note);
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid HomeId { get; private set; }
    public BookmarkStage Stage { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public void Update(BookmarkStage stage, string? note, DateTimeOffset now)
    {
        Stage = stage;
        Note = NormalizeNote(note);
        UpdatedAt = now;
    }

    /// <summary>Combines this note with another Bookmark's note, marking where the second began.</summary>
    public string? JoinNote(Bookmark other, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(other.Note))
        {
            return Note;
        }

        if (string.IsNullOrWhiteSpace(Note))
        {
            return other.Note;
        }

        var joined = $"{Note}\n\n--- merged {now:yyyy-MM-dd} ---\n{other.Note}";
        return joined.Length <= NoteMaxLength ? joined : joined[..NoteMaxLength];
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= NoteMaxLength
            ? trimmed
            : throw new DomainRuleException(
                "note",
                $"Note must be at most {NoteMaxLength} characters."
            );
    }
}
