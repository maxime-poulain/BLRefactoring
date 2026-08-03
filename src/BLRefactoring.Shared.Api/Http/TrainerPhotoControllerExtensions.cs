using BLRefactoring.Shared.Application.Dtos.Trainer;
using BLRefactoring.Shared.Common.Errors;
using BLRefactoring.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BLRefactoring.Shared.Api.Http;

/// <summary>
/// The HTTP shape of a trainer's photo, written once for both hosts.
/// </summary>
/// <remarks>
/// Both hosts publish the same operations, so both answer identically or the parity is cosmetic.
/// Everything that could drift — which failure is which status, how the response is cached, what
/// the ETag is cut from — lives here rather than twice in two controllers.
/// </remarks>
public static class TrainerPhotoControllerExtensions
{
    /// <summary>
    /// The largest request body a photo upload may carry.
    /// </summary>
    /// <remarks>
    /// The photo limit plus room for the multipart envelope — boundaries, part headers, the field
    /// name. Sizing this to the photo alone would refuse a file of exactly the advertised maximum,
    /// which is the sort of limit that is off by a few hundred bytes and impossible to explain.
    /// </remarks>
    public const long MaxRequestBytes = TrainerPhoto.MaxSizeInBytes + (8 * 1024);

    /// <summary>
    /// Writes a photo to the response, cached hard and tagged.
    /// </summary>
    /// <param name="controller">The controller answering.</param>
    /// <param name="photo">The photo to serve.</param>
    /// <returns>The bytes, or 304 when the caller already has them.</returns>
    /// <remarks>
    /// <para>
    /// Cached for a year and marked immutable, which is only honest because the address changes
    /// when the picture does: a replacement mints a new photo identity, so the bytes under any one
    /// ETag genuinely never change. This is what lets a CDN sit in front of this route later
    /// without a line of code moving.
    /// </para>
    /// <para>
    /// The tag is the photo's identity, not the row's version. Those are different facts — the
    /// version changes when a trainer edits their name, and re-downloading a portrait because
    /// somebody fixed a typo in their surname is waste with no upside.
    /// </para>
    /// </remarks>
    public static ActionResult PhotoFile(this ControllerBase controller, TrainerPhotoDto photo)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(photo);

        controller.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromDays(365),
            Extensions = { new NameValueHeaderValue("immutable") }
        };

        // This overload answers If-None-Match itself, so a client holding the current tag gets a
        // 304 without the bytes travelling again.
        return controller.File(
            photo.Content.ToArray(),
            photo.ContentType,
            fileDownloadName: null,
            lastModified: null,
            entityTag: new EntityTagHeaderValue($"\"{photo.PhotoId}\""));
    }

    /// <summary>
    /// Turns a failed photo operation into the status that describes it.
    /// </summary>
    /// <param name="controller">The controller answering.</param>
    /// <param name="errors">Why the operation failed.</param>
    /// <returns>The problem response.</returns>
    public static ActionResult PhotoProblem(
        this ControllerBase controller,
        IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(errors);

        var collected = errors as IReadOnlyList<Error> ?? [.. errors];

        return controller.Problem(StatusFor(collected), collected);
    }

    /// <remarks>
    /// Only the kernel's codes are named here. The aggregate's own — why an image was refused —
    /// all mean the same thing at this boundary: the caller sent something this API will not take,
    /// which is 400, and the code itself travels in the body for a client that wants to say more.
    /// Reaching for <c>TrainerErrorCodes</c> would put a domain type in the HTTP layer, two layers
    /// out from where it belongs.
    /// </remarks>
    private static int StatusFor(IReadOnlyList<Error> errors)
    {
        if (errors.Any(error => error.ErrorCode == ErrorCodes.NotFound))
        {
            return StatusCodes.Status404NotFound;
        }

        // 409 rather than 412: nothing was asked of the caller as a precondition, so there is no
        // precondition to have failed. Two uploads simply raced and this one lost.
        return errors.Any(error => error.ErrorCode == ErrorCodes.ConcurrencyConflict)
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
    }

    /// <summary>
    /// Reads an uploaded file into memory.
    /// </summary>
    /// <param name="file">The uploaded part.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes.</returns>
    /// <remarks>
    /// Buffered because the whole point of the next step is to look at the first bytes and the
    /// length, and because the request size limit already bounds what can arrive.
    /// </remarks>
    public static async Task<byte[]> ReadAllBytesAsync(
        this IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var buffer = new MemoryStream();
        await using var upload = file.OpenReadStream();

        await upload.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}
