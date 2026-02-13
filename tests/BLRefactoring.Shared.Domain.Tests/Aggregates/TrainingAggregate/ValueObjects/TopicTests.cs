using BLRefactoring.Shared.Domain.Aggregates.TrainingAggregate.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BLRefactoring.Shared.Domain.Tests.Aggregates.TrainingAggregate.ValueObjects;

public class TopicTests
{
    [Fact]
    public void FromName_Programming_ReturnsCorrectTopic()
    {
        // Act
        var topic = Topic.FromName("Programming");

        // Assert
        topic.Name.Should().Be("Programming");
    }

    [Fact]
    public void FromName_CaseInsensitive_ReturnsCorrectTopic()
    {
        // Act
        var topic = Topic.FromName("programming");

        // Assert
        topic.Should().Be(Topic.Programming);
    }

    [Fact]
    public void FromName_InvalidName_ThrowsArgumentException()
    {
        // Act
        var act = () => Topic.FromName("InvalidTopic");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetTopics_ReturnsAllSixTopics()
    {
        // Act
        var topics = Topic.GetTopics();

        // Assert
        topics.Should().HaveCount(6);
    }

    [Fact]
    public void Equality_SameName_AreEqual()
    {
        // Arrange
        var topic1 = Topic.FromName("Programming");
        var topic2 = Topic.FromName("Programming");

        // Assert
        topic1.Should().Be(topic2);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        // Arrange
        var topic1 = Topic.Programming;
        var topic2 = Topic.Design;

        // Assert
        topic1.Should().NotBe(topic2);
    }

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
