using TrainingHub.Shared.Application.IntegrationEvents;
using TrainingHub.Architecture.Tests.Framework;
using Xunit;

namespace TrainingHub.Architecture.Tests.Rules;

/// <summary>
/// The erasure's farewell (ADR 0085), held to the property the whole notice depends on: the fact
/// carries its own destination.
/// </summary>
/// <remarks>
/// The transaction and the password gate are held next door, in <see cref="TransactionRules"/>,
/// beside the registration they mirror. What this file holds is the fact's shape — because the
/// sanction notices resolve their address at delivery and a refactoring toward that familiar
/// pattern would compile, pass every unit test that mocks the sender, and mail nobody: the row
/// the address would be resolved from is the row this fact announces the death of.
/// </remarks>
public sealed class AccountErasureRules
{
    /// <summary>
    /// The farewell, carries its address.
    /// </summary>
    [Fact]
    [ArchitectureRule("0085",
        "the erasure's fact carries the address it will be delivered to, because gone is the " +
        "only state the fact is ever true in and nothing can be asked at delivery")]
    public void TheFarewell_CarriesItsAddress()
    {
        var expected = new[]
        {
            nameof(AccountErasedIntegrationEvent.UserId),
            nameof(AccountErasedIntegrationEvent.Email),
            nameof(AccountErasedIntegrationEvent.Username),
        };

        var properties = typeof(AccountErasedIntegrationEvent).GetProperties();

        properties
            .Selected("property the erasure's fact declares")
            .Where(property => !expected.Contains(property.Name, StringComparer.Ordinal))
            .Select(property =>
                $"AccountErasedIntegrationEvent.{property.Name} is not one of the three members " +
                "ADR 0085 names — the account, the address the farewell goes to, and the name it " +
                "greets")
            .Concat(properties.Any(property => property.Name == nameof(AccountErasedIntegrationEvent.Email))
                ? []
                : new[]
                {
                    "AccountErasedIntegrationEvent no longer declares Email. A farewell that " +
                    "resolves its address at delivery resolves it from a row this fact announces " +
                    "the death of, and mails nobody",
                })
            .ShouldHold();
    }
}
