using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TrainingHub.Shared.Api.Contracts.Trainers;

/// <summary>
/// The multipart body of a photo upload.
/// </summary>
/// <remarks>
/// A class with one file on it rather than a bare <see cref="IFormFile"/> parameter: the document
/// generator describes a bound model, so this is what gives the generated clients a named part
/// instead of an untyped stream.
/// </remarks>
public sealed class UploadTrainerPhotoHttpRequest
{
    /// <summary>
    /// The image being published.
    /// </summary>
    [Required]
    public IFormFile Photo { get; init; } = null!;
}
