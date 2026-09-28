namespace Domolov.Domain.Homes;

/// <summary>Word trigram similarity in the style of PostgreSQL pg_trgm (Jaccard over trigram sets).</summary>
public static class TrigramSimilarity
{
    public static double Compute(string a, string b)
    {
        var ta = Trigrams(a);
        var tb = Trigrams(b);
        if (ta.Count == 0 || tb.Count == 0)
        {
            return 0;
        }

        var intersection = ta.Count(tb.Contains);
        var union = ta.Count + tb.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    public static HashSet<string> Trigrams(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var padded = $"  {word} ";
            for (var i = 0; i + 3 <= padded.Length; i++)
            {
                set.Add(padded.Substring(i, 3));
            }
        }

        return set;
    }
}
