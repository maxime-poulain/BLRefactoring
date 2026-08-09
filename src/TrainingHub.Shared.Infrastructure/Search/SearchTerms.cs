using System.Text;

namespace TrainingHub.Shared.Infrastructure.Search;

/// <summary>
/// Turns a title into the tokens the index is seeked by, and a query into the tokens it seeks with.
/// </summary>
/// <remarks>
/// The index's own business, so it lives behind the port with the rest of it: nothing outside this
/// folder knows that a title is stored twice, once to read and once to find (ADR 0059). Writing and
/// reading tokenize through the same method on purpose — a search that normalized its term
/// differently from the index would answer nothing and look like an empty catalogue.
/// </remarks>
public static class SearchTerms
{
    /// <summary>
    /// How many tokens one text contributes.
    /// </summary>
    /// <remarks>
    /// A bound rather than a policy about language. A hundred-character title can hold fifty words,
    /// and a search matching all of them is fifty joins for a question nobody asked; the tokens past
    /// this are dropped from both sides, so the two still agree.
    /// </remarks>
    public const int MaximumCount = 10;

    /// <summary>
    /// The shortest token worth an index row.
    /// </summary>
    /// <remarks>
    /// A single letter matches most of the catalogue by prefix, which costs a row to store and
    /// answers nothing useful. Two is where a word starts being a word.
    /// </remarks>
    public const int MinimumLength = 2;

    /// <summary>
    /// Splits a text into its distinct, normalized, bounded tokens.
    /// </summary>
    /// <param name="text">The title being indexed, or the term being searched for.</param>
    /// <returns>The tokens, upper-cased, deduplicated and capped at <see cref="MaximumCount"/>.</returns>
    /// <remarks>
    /// A word is a run of letters or digits, which is a rule about writing rather than about French
    /// or English: it separates on the punctuation of every alphabet without a list to keep. The
    /// tokens come out upper-cased rather than lower-cased because a round trip through lower case
    /// is not lossless in every culture, which is what CA1308 says out loud — and invariantly, so
    /// that the tokens a title yields do not depend on the machine that indexed it.
    /// </remarks>
    public static IReadOnlyList<string> Of(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var tokens = new List<string>();
        var word = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                word.Append(character);
                continue;
            }

            Close(word, tokens);
        }

        Close(word, tokens);

        return tokens;
    }

    /// <summary>
    /// Ends the word being read and keeps it, unless it is too short, a repeat, or one too many.
    /// </summary>
    private static void Close(StringBuilder word, List<string> tokens)
    {
        if (word.Length >= MinimumLength && tokens.Count < MaximumCount)
        {
            var token = word.ToString().ToUpperInvariant();

            if (!tokens.Contains(token, StringComparer.Ordinal))
            {
                tokens.Add(token);
            }
        }

        word.Clear();
    }
}
