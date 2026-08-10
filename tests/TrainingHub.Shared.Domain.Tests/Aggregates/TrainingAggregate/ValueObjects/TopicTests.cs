using System.Reflection;
using AwesomeAssertions;
using TrainingHub.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using Xunit;

namespace TrainingHub.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

/// <summary>
/// Behavior covered for <c>Topic</c>.
/// </summary>
public sealed class TopicTests
{
    /// <summary>
    /// Get topics, contains every topic declared on the type.
    /// </summary>
    [Fact]
    public void GetTopics_ContainsEveryTopicDeclaredOnTheType()
    {
        // The set behind GetTopics() is written by hand. A topic declared as a field but left out
        // of it would be invisible to the whole application: TryFromName would reject its name, no
        // training could carry it, no query could filter on it — and no test would notice, since
        // any test that restates the expected list is wrong in exactly the same way.
        // Reflection is the one reader that cannot forget, because it reads the declarations
        // themselves. It runs here, once, at test time — it used to run in the domain, rebuilding
        // the set on first use.
        var declared = typeof(Topic)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(Topic))
            .Select(field => (Topic)field.GetValue(null)!);

        // Equivalence rather than equality: reflection makes no promise about field order, and
        // this way the assertion also fails on a topic listed in the set but no longer declared.
        Topic.GetTopics().Should().BeEquivalentTo(declared);
    }

    /// <summary>
    /// Get topics, hands out a set the caller cannot change.
    /// </summary>
    [Fact]
    public void GetTopics_HandsOutASetTheCallerCannotChange()
    {
        // The set is static: a caller who emptied it would empty it for every other caller in
        // the process. GetTopics() used to hand out the very List it had cached.
        var topics = Topic.GetTopics();
        var countBefore = topics.Count;

        var clear = () => ((ICollection<Topic>)topics).Clear();

        clear.Should().Throw<NotSupportedException>();
        // Deliberately not pinned to a number: adding a topic is a legitimate change, and it
        // should not have to be reflected in a test about immutability.
        Topic.GetTopics().Should().HaveCount(countBefore);
    }

    /// <summary>
    /// Equality, same name, are equal.
    /// </summary>
    [Fact]
    public void Equality_SameName_AreEqual()
    {
        // Arrange
        Topic.TryFromName("Programming", out var topic1);
        Topic.TryFromName("Programming", out var topic2);

        // Assert
        topic1.Should().Be(topic2);
    }

    /// <summary>
    /// Equality, different name, are not equal.
    /// </summary>
    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        // Arrange
        var topic1 = Topic.Programming;
        var topic2 = Topic.Design;

        // Assert
        topic1.Should().NotBe(topic2);
    }

    /// <summary>
    /// Try from name, known name, returns true and topic.
    /// </summary>
    [Fact]
    public void TryFromName_KnownName_ReturnsTrueAndTopic()
    {
        // Act
        var found = Topic.TryFromName("Programming", out var topic);

        // Assert
        found.Should().BeTrue();
        topic.Should().Be(Topic.Programming);
    }

    /// <summary>
    /// Try from name, is case sensitive.
    /// </summary>
    [Fact]
    public void TryFromName_IsCaseSensitive()
    {
        // Topics are a closed enumeration fed by GetTopics(): a name with the
        // wrong casing means a misbehaving client and must be rejected.
        var found = Topic.TryFromName("programming", out var topic);

        // Assert
        found.Should().BeFalse();
        topic.Should().BeNull();
    }

    /// <summary>
    /// Try from name, unknown name, returns false and null.
    /// </summary>
    [Fact]
    public void TryFromName_UnknownName_ReturnsFalseAndNull()
    {
        // Act
        var found = Topic.TryFromName("Underwater Basket Weaving", out var topic);

        // Assert
        found.Should().BeFalse();
        topic.Should().BeNull();
    }

    /// <summary>
    /// Static instances, have expected names.
    /// </summary>
    [Fact]
    public void StaticInstances_HaveExpectedNames()
    {
        // Assert
        Topic.Programming.Name.Should().Be("Programming");
        Topic.Design.Name.Should().Be("Design");
        Topic.Marketing.Name.Should().Be("Marketing");
        Topic.Business.Name.Should().Be("Business");
        Topic.PersonalDevelopment.Name.Should().Be("Personal Development");
        Topic.Leadership.Name.Should().Be("Leadership");
    }
}
