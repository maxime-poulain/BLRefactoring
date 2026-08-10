using AwesomeAssertions;
using Moq;
using TrainingHub.DDDWithCqrs.Application.Features.Catalog.GetTrainerProfile;
using TrainingHub.DDDWithCqrs.Infrastructure.Features.Catalog.GetTrainerProfile;
using TrainingHub.Shared.Application.Catalog;
using TrainingHub.Shared.Application.Dtos.Trainer;
using Xunit;

namespace TrainingHub.DDDWithCqrs.Tests.Handlers;

/// <summary>
/// Behavior covered for <c>GetTrainerProfileQueryHandler</c>.
/// </summary>
/// <remarks>
/// The twin of <c>CatalogApplicationServiceTests</c>' profile facts, deliberately: what both hosts
/// must do with a read model is arrive at the same port with the same arguments, and a fact that
/// exists on only one of them says nothing about the parity ADR 0006 promises. What the port then
/// does with the two authorities is exercised against a real database in
/// <c>CatalogDetailQueryTests</c>.
/// </remarks>
public sealed class GetTrainerProfileQueryHandlerTests
{
    private readonly Mock<ICatalogDetailQuery> _catalogDetail = new();

    /// <summary>
    /// Handle, an identifier, asks the detail port for exactly it.
    /// </summary>
    [Fact]
    public async Task Handle_AnIdentifier_AsksTheDetailPortForExactlyIt()
    {
        var trainerId = Guid.CreateVersion7();

        var sut = new GetTrainerProfileQueryHandler(_catalogDetail.Object);

        await sut.Handle(new GetTrainerProfileQuery(trainerId), CancellationToken.None);

        _catalogDetail.Verify(
            detail => detail.FindOfferedTrainerAsync(trainerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Handle, what the port answered, hands it back untouched.
    /// </summary>
    /// <remarks>
    /// No projection on the way out, like its neighbor's reason: the port already answers the
    /// published shape, and rebuilding it here would be a second place where a name could go
    /// missing.
    /// </remarks>
    [Fact]
    public async Task Handle_WhatThePortAnswered_HandsItBackUntouched()
    {
        var answered = new CatalogTrainerDto
        {
            Id = Guid.CreateVersion7(),
            Firstname = "Ada",
            Lastname = "Lovelace",
            Bio = "Wrote the first program.",
            Trainings = []
        };

        _catalogDetail
            .Setup(detail => detail.FindOfferedTrainerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answered);

        var sut = new GetTrainerProfileQueryHandler(_catalogDetail.Object);

        var profile = await sut.Handle(
            new GetTrainerProfileQuery(Guid.CreateVersion7()),
            CancellationToken.None);

        profile.Should().BeSameAs(answered);
    }

    /// <summary>
    /// Handle, nobody to show, hands the nothing back.
    /// </summary>
    [Fact]
    public async Task Handle_NobodyToShow_HandsTheNothingBack()
    {
        _catalogDetail
            .Setup(detail => detail.FindOfferedTrainerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogTrainerDto?)null);

        var sut = new GetTrainerProfileQueryHandler(_catalogDetail.Object);

        var profile = await sut.Handle(
            new GetTrainerProfileQuery(Guid.CreateVersion7()),
            CancellationToken.None);

        profile.Should().BeNull();
    }
}
