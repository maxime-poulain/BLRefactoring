using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using TrainingHub.Blazor.Client.Models;
using TrainingHub.Translations;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Models;

/// <summary>
/// The translation from a topic's value to the key its name is filed under (ADR 0091).
/// </summary>
/// <remarks>
/// Two claims, and the second is the one worth a localizer. That the derivation lands on the key
/// the family declares — asserted through the real resource files, because a helper that agreed
/// with a hand-written expectation and disagreed with the resx would show
/// <c>Topic_CloudComputing</c> on four screens. And that the value never moves: the English name
/// is what the filter, the contract and the search index carry, so a screen translating what it
/// sends would break the round trip the URL depends on.
/// <para>
/// The completeness of the family in both directions is a different question, and one this project
/// cannot ask — it references the browser and not the domain.
/// <c>EveryTopic_IsNamedByTheTranslations</c> asks it from the suite that can see both.
/// </para>
/// </remarks>
public sealed class TopicNamesTests
{
    private readonly IStringLocalizer<TopicResources> _topics =
        new ServiceCollection()
            .AddLogging()
            .AddLocalization()
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<TopicResources>>();

    /// <summary>
    /// Key, a topic whose name carries spaces, closes them up in pascal case.
    /// </summary>
    [Theory]
    [InlineData("Programming", "Topic_Programming")]
    [InlineData("DevOps", "Topic_DevOps")]
    [InlineData("Personal Development", "Topic_PersonalDevelopment")]
    [InlineData("Data and Analytics", "Topic_DataAndAnalytics")]
    [InlineData("Testing and Quality", "Topic_TestingAndQuality")]
    public void Key_ATopicWhoseNameCarriesSpaces_ClosesThemUpInPascalCase(string topic, string expected) =>
        TopicNames.Key(topic).Should().Be(expected);

    /// <summary>
    /// Key, every topic the picker offers, names a translation rather than itself.
    /// </summary>
    /// <remarks>
    /// A localizer answers the key when the family does not declare it, so "the value differs from
    /// the key" is exactly the assertion that catches a derivation nobody translated.
    /// </remarks>
    [Fact]
    public void Key_EveryTopicThePickerOffers_NamesATranslationRatherThanItself()
    {
        foreach (var topic in Topic.All)
        {
            var key = TopicNames.Key(topic);

            _topics[key].ResourceNotFound.Should().BeFalse(
                "{0} offers '{1}', so TopicResources has to name it", nameof(Topic), topic);
        }
    }

    /// <summary>
    /// The translated name, differs from the value the request carries.
    /// </summary>
    /// <remarks>
    /// The half ADR 0091 is most easily lost: a screen that started sending what it displays would
    /// send <c>Développement web</c> to a filter that admits <c>Web Development</c>, and the URL
    /// would stop round-tripping.
    /// </remarks>
    [Fact]
    public void TheTranslatedName_DiffersFromTheValueTheRequestCarries()
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");

        try
        {
            _topics[TopicNames.Key("Web Development")].Value.Should().Be("Développement web");
            _topics[TopicNames.Key("Cloud Computing")].Value.Should().Be("Informatique en nuage");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }
}
