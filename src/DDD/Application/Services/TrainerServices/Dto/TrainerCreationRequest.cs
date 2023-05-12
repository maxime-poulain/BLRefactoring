namespace BLRefactoring.DDD.Application.Services.TrainerServices.Dto;

public sealed class TrainerCreationRequest
{
    public string Firstname { get; init; } = null!;
    public string Lastname { get; init; } = null!;
    public string Email { get; init; } = null!;
}