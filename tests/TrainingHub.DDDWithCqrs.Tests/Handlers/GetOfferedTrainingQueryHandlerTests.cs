using AwesomeAssertions;
using Moq;
using TrainingHub.DDDWithCqrs.Application.Features.Catalogue.GetOffered;
using TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalogue.GetOffered;
using TrainingHub.Shared.Application.Catalogue;
using TrainingHub.Shared.Application.Dtos.Training;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behaviour covered for <c>GetOfferedTrainingQueryHandler</c>.
/// </summary>
/// <remarks>
/// The twin of <c>CatalogueApplicationServiceTests</c>' last two facts, deliberately: what both
/// hosts must do with a read model is arrive at the same port with the same arguments, and a fact
/// that exists on only one of them says nothing about the parity ADR 0006 promises. What the port
/// then does with the two authorities is exercised against a real database in
/// <c>CatalogueDetailQueryTests</c>.
/// </remarks>
public sealed class GetOfferedTrainingQueryHandlerTests
{
    private readonly Mock<ICatalogueDetailQuery> _catalogueDetail = new();

    /// <summary>
    /// Handle, an identifier, asks the detail port for exactly it.
    /// </summary>
    [Fact]
    public async Task Handle_AnIdentifier_AsksTheDetailPortForExactlyIt()
    {
        var trainingId = Guid.CreateVersion7();

        var sut = new GetOfferedTrainingQueryHandler(_catalogueDetail.Object);

        await sut.Handle(new GetOfferedTrainingQuery(trainingId), CancellationToken.None);

        _catalogueDetail.Verify(
            detail => detail.FindOfferedAsync(trainingId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, what the port answered, hands it back untouched.
    /// </summary>
    /// <remarks>
    /// No projection on the way out, unlike this handler's neighbours: the port already answers the
    /// published shape, and rebuilding it here would be a second place where the trainer's name
    /// could go missing.
    /// </remarks>
    [Fact]
    public async Task Handle_WhatThePortAnswered_HandsItBackUntouched()
    {
        var answered = new CatalogueTrainingDetailDto
        {
            Id = Guid.CreateVersion7(),
            Title = "Domain Driven Design",
            TrainerName = "Ada Lovelace",
            Topics = ["Architecture"],
            Description = "What a bounded context is.",
            Prerequisites = "None.",
            AcquiredSkills = "Drawing a context map."
        };

        _catalogueDetail
            .Setup(detail => detail.FindOfferedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = new GetOfferedTrainingQueryHandler(_catalogueDetail.Object);

        var offered = await sut.Handle(
            new GetOfferedTrainingQuery(Guid.CreateVersion7()),
            CancellationToken.None);

        offered.Should().BeSameAs(answered);
    }

    /// <summary>
    /// Handle, nothing on offer, hands the nothing back.
    /// </summary>
    [Fact]
    public async Task Handle_NothingOnOffer_HandsTheNothingBack()
    {
        _catalogueDetail
            .Setup(detail => detail.FindOfferedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogueTrainingDetailDto?)null);

        var sut = new GetOfferedTrainingQueryHandler(_catalogueDetail.Object);

        var offered = await sut.Handle(
            new GetOfferedTrainingQuery(Guid.CreateVersion7()),
            CancellationToken.None);

        offered.Should().BeNull();
    }
}
