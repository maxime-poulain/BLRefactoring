using AwesomeAssertions;
using TrainingHub.Shared.Infrastructure.Search;
using Xunit;

namespace TrainingHub.Shared.Infrastructure.Tests.Search;

/// <summary>
/// What a title becomes on its way into the index, and what a search term becomes on its way out.
/// </summary>
/// <remarks>
/// Both directions go through the same method, so these facts are about both: a normalization that
/// differed between the two would answer nothing and look like an empty catalog rather than like
/// a defect.
/// </remarks>
public sealed class SearchTermsTests
{
    /// <summary>
    /// Of, a title, splits it into upper-cased words.
    /// </summary>
    /// <remarks>
    /// Upper rather than lower, which is CA1308's point rather than a preference: lower-casing is
    /// not a lossless round trip in every culture.
    /// </remarks>
    [Fact]
    public void Of_ATitle_SplitsItIntoUpperCasedWords() =>
        SearchTerms.Of("Domain Driven Design").Should().Equal("DOMAIN", "DRIVEN", "DESIGN");

    /// <summary>
    /// Of, punctuation and accents, separates on the first and keeps the second.
    /// </summary>
    /// <remarks>
    /// A word is a run of letters or digits, so the rule is about writing rather than about a
    /// language: an apostrophe separates and an accented letter does not.
    /// </remarks>
    [Fact]
    public void Of_PunctuationAndAccents_SeparatesOnTheFirstAndKeepsTheSecond() =>
        SearchTerms.Of("L'événement, revisité (2e édition)")
            .Should().Equal("ÉVÉNEMENT", "REVISITÉ", "2E", "ÉDITION");

    /// <summary>
    /// Of, a word repeated, indexes it once.
    /// </summary>
    /// <remarks>
    /// The tokens are the composite key of their table, so a duplicate would not merely be waste —
    /// it would fail the insert.
    /// </remarks>
    [Fact]
    public void Of_AWordRepeated_IndexesItOnce() =>
        SearchTerms.Of("Testing testing TESTING").Should().ContainSingle().Which.Should().Be("TESTING");

    /// <summary>
    /// Of, a single letter, drops it.
    /// </summary>
    /// <remarks>
    /// A one-letter prefix matches most of the catalog, which costs a row and answers nothing.
    /// </remarks>
    [Fact]
    public void Of_ASingleLetter_DropsIt() =>
        SearchTerms.Of("A guide to C").Should().Equal("GUIDE", "TO");

    /// <summary>
    /// Of, more words than the bound, keeps the first ten.
    /// </summary>
    /// <remarks>
    /// The bound applies on both sides, so a title and a term that both overflow still agree about
    /// their first ten words. Asserted on the count and on the last one kept, because a cap that
    /// silently kept the wrong end would pass a count-only assertion.
    /// </remarks>
    [Fact]
    public void Of_MoreWordsThanTheBound_KeepsTheFirstTen()
    {
        var tokens = SearchTerms.Of("un deux trois quatre cinq six sept huit neuf dix onze douze");

        tokens.Should().HaveCount(SearchTerms.MaximumCount);
        tokens[^1].Should().Be("DIX");
    }

    /// <summary>
    /// Of, nothing worth a token, answers nothing.
    /// </summary>
    /// <remarks>
    /// Three ways of saying the same thing, because the query treats "no tokens" as "no filter":
    /// a term made only of punctuation must not quietly become a catalog listing by a different
    /// route than a blank one.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ??? -")]
    public void Of_NothingWorthAToken_AnswersNothing(string? text) =>
        SearchTerms.Of(text).Should().BeEmpty();
}
