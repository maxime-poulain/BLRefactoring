using AwesomeAssertions;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TrainingHub.Blazor.Client.Components;
using Xunit;

namespace TrainingHub.Blazor.Client.Tests.Components;

/// <summary>
/// Behaviour covered for the redirect an unauthenticated visitor lands on.
/// </summary>
/// <remarks>
/// Two things live here and nowhere else. The address the visitor asked for is carried into
/// <c>returnUrl</c> so a deep link survives signing in — without it every bookmark and every
/// expired session lands on the catalogue. And the address is made relative before it is carried,
/// which is what stops a sign-in page from becoming a phishing hop: a redirect target taken from
/// the address bar and replayed after authentication is an open redirect the moment it is allowed
/// to name a host.
/// </remarks>
public sealed class RedirectToLoginTests : ComponentTest
{
    /// <summary>
    /// Initialised, a deep link, remembers where the visitor was going.
    /// </summary>
    [Fact]
    public void Initialised_ADeepLink_RemembersWhereTheVisitorWasGoing()
    {
        // Arrange
        var navigation = Navigation("http://localhost/trainings/create");

        // Act
        Render<RedirectToLogin>();

        // Assert
        navigation.Uri.Should().Be("http://localhost/login?returnUrl=%2Ftrainings%2Fcreate");
    }

    /// <summary>
    /// Initialised, at the root, has nothing to remember.
    /// </summary>
    [Fact]
    public void Initialised_AtTheRoot_HasNothingToRemember()
    {
        // Arrange
        var navigation = Navigation("http://localhost/");

        // Act
        Render<RedirectToLogin>();

        // Assert
        navigation.Uri.Should().Be("http://localhost/login");
    }

    /// <summary>
    /// Initialised, the address carries a query, escapes it into one parameter.
    /// </summary>
    /// <remarks>
    /// Unescaped, the requested query string would merge into the sign-in page's own and the part
    /// after the first ampersand would be read as a second parameter of the login address.
    /// </remarks>
    [Fact]
    public void Initialised_TheAddressCarriesAQuery_EscapesItIntoOneParameter()
    {
        // Arrange
        var navigation = Navigation("http://localhost/trainings?page=2&size=10");

        // Act
        Render<RedirectToLogin>();

        // Assert
        navigation.Uri.Should().Be("http://localhost/login?returnUrl=%2Ftrainings%3Fpage%3D2%26size%3D10");
    }

    private BunitNavigationManager Navigation(string uri)
    {
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(uri);

        return navigation;
    }
}
