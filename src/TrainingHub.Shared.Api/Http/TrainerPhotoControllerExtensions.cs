using TrainingHub.Shared.Application.Dtos.Trainer;
using TrainingHub.Shared.Common.Errors;
using TrainingHub.Shared.Domain.Aggregates.TrainerAggregate.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace TrainingHub.Shared.Api.Http;

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
    /// Writes a photo to the response, cached for a year and revalidated by its tag.
    /// </summary>
    /// <param name="controller">The controller answering.</param>
    /// <param name="photo">The photo to serve.</param>
    /// <returns>The bytes, or 304 when the caller already has them.</returns>
    /// <remarks>
    /// <para>
    /// For an address that does <em>not</em> name the photo — <c>GET /Trainer/{id}/photo</c>, whose
    /// bytes change the moment its owner uploads a new portrait. It used to say <c>immutable</c>
    /// too, and that was wrong: <c>immutable</c> promises the response body for this URL will never
    /// change, and a browser that believes it stops asking for a year. Two callers of one helper is
    /// how that promise got made on behalf of an address that cannot keep it.
    /// </para>
    /// <para>
    /// What is left is a long <c>max-age</c> with an <c>ETag</c>, so a stale cache revalidates and
    /// is answered 304 when nothing moved. See <see cref="ImmutablePhotoFile"/> for the address that
    /// can make the stronger promise, and ADR 0063.
    /// </para>
    /// <para>
    /// The tag is the photo's identity, not the row's version. Those are different facts — the
    /// version changes when a trainer edits their name, and re-downloading a portrait because
    /// somebody fixed a typo in their surname is waste with no upside.
    /// </para>
    /// </remarks>
    public static ActionResult PhotoFile(this ControllerBase controller, TrainerPhotoDto photo) =>
        WritePhoto(controller, photo, immutable: false);

    /// <summary>
    /// Writes a photo to the response, cached for a year and never revalidated.
    /// </summary>
    /// <param name="controller">The controller answering.</param>
    /// <param name="photo">The photo to serve.</param>
    /// <returns>The bytes, or 304 when the caller already has them.</returns>
    /// <remarks>
    /// For the public portrait, whose address carries the photo's identity —
    /// <c>GET /Catalogue/trainings/{trainingId}/photo/{photoId}</c>. A replacement mints a new
    /// identity, so <em>this</em> URL's bytes genuinely never change and <c>immutable</c> is a
    /// promise the address keeps by construction. That is what lets a CDN sit in front of the route
    /// later without a line of code moving (ADR 0063).
    /// <para>
    /// Two named helpers rather than one taking a flag: which promise an endpoint makes about its
    /// own bytes is a property of its address, and a caller passing <c>true</c> is a caller who has
    /// to have thought about it. One did not.
    /// </para>
    /// </remarks>
    public static ActionResult ImmutablePhotoFile(this ControllerBase controller, TrainerPhotoDto photo) =>
        WritePhoto(controller, photo, immutable: true);

    private static ActionResult WritePhoto(ControllerBase controller, TrainerPhotoDto photo, bool immutable)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(photo);

        var cacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromDays(365)
        };

        if (immutable)
        {
            cacheControl.Extensions.Add(new NameValueHeaderValue("immutable"));
        }

        controller.Response.GetTypedHeaders().CacheControl = cacheControl;

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
