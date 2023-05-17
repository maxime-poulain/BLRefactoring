using BLRefactoring.FastEndpoints.Application.Features.Trainers.Create.Command;
using FastEndpoints;

namespace BLRefactoring.FastEndpoints.Application.Features.Trainers.Create;

// Endpoints could be part of the Api project
// I put `CreateTrainerEndpoint` in the Trainers/Create slice
// to illustrate that this is possible.

public class CreateTrainerEndpoint : Endpoint<CreateTrainerCommand, TrainerDto>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/trainer");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateTrainerCommand command, CancellationToken ct)
    {
        await SendAsync(await command.ExecuteAsync(ct), cancellation: ct);
    }
}
