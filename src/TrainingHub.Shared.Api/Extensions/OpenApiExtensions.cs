using System.Reflection;
using TrainingHub.Shared.Api.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
// Microsoft.OpenApi v2, which .NET 10 carries, flattened the old Microsoft.OpenApi.Models
// namespace into this one.
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace TrainingHub.Shared.Api.Extensions;

/// <summary>
/// One OpenAPI document generator for both hosts, and one place where the API describes how it is
/// authenticated.
/// </summary>
/// <remarks>
/// Shared for the same reason CORS, identity, optimistic concurrency and problem details are: the
/// two hosts advertise the same REST API. They did not describe it the same way — one generated its
/// document with NSwag, the other with Swashbuckle — and the two libraries disagree on the details
/// that matter to a generated client, starting with nullability and enum representation. See
/// ADR 0006.
/// </remarks>
public static class OpenApiExtensions
{
    /// <summary>
    /// Registers the framework's OpenAPI document, with the bearer scheme declared on it.
    /// </summary>
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        return services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddDocumentTransformer<UploadedFileTransformer>();
            options.AddOperationTransformer<OperationIdTransformer>();
            options.AddOperationTransformer<EntityTagTransformer>();
        });
    }

    /// <summary>
    /// Serves the document and the reference UI.
    /// </summary>
    /// <remarks>
    /// Both hosts call this from their Development branch only, as they did before: an API does not
    /// need to publish its own documentation endpoint in production to be documented.
    /// </remarks>
    public static WebApplication UseApiOpenApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapOpenApi();
        app.MapScalarApiReference();

        return app;
    }
}

/// <summary>
/// Gives every operation an identifier of the form <c>Controller_Action</c>.
/// </summary>
/// <remarks>
/// <c>Microsoft.AspNetCore.OpenApi</c> emits no <c>operationId</c> at all, and the absence is not
/// cosmetic: it is the only stable name an operation has, and every client generator keys off it.
/// Without them, NSwag falls back to deriving names from paths and verbs, and the result is what a
/// build log showed before this transformer existed — <c>TrainingDELETEAsync</c>,
/// <c>MeGETAsync</c>, and an <c>All2Async</c> whose "2" exists only because a first <c>AllAsync</c>
/// had already been taken by a different controller.
/// <para>
/// It also decides how many clients get generated. <c>MultipleClientsFromOperationId</c> splits on
/// the part before the underscore, so no identifiers means one giant client instead of one per
/// controller.
/// </para>
/// <para>
/// The action name has already lost its <c>Async</c> suffix by the time it reaches here — MVC
/// strips it by convention — so <c>CreateTrainingAsync</c> becomes <c>Training_CreateTraining</c>,
/// and the generated method is <c>TrainingClient.CreateTrainingAsync</c>.
/// </para>
/// </remarks>
internal sealed class OperationIdTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Description.ActionDescriptor is ControllerActionDescriptor action)
        {
            operation.OperationId = $"{action.ControllerName}_{action.ActionName}";
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Puts the <c>ETag</c> on the responses of the actions that publish one.
/// </summary>
/// <remarks>
/// The framework reflects an action's parameters into the document and its response bodies, but a
/// response *header* is set inside the method body and there is nothing for it to see — no
/// attribute in ASP.NET Core declares one. So this reads the marker
/// <see cref="ProducesEntityTagAttribute"/> and writes the header in.
/// <para>
/// Why it matters is on the attribute: the request half of this contract was undeclared for the
/// same kind of reason, and the generated client consequently could not send an <c>If-Match</c> at
/// all. See ADR 0010.
/// </para>
/// </remarks>
internal sealed class EntityTagTransformer : IOpenApiOperationTransformer
{
    private const string SuccessStatusCode = "200";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action ||
            action.MethodInfo.GetCustomAttribute<ProducesEntityTagAttribute>() is null)
        {
            return Task.CompletedTask;
        }

        // Only the success response carries it. A 404 or a 412 describes a resource the caller is
        // not being handed, so there is no version to state.
        if (operation.Responses?.TryGetValue(SuccessStatusCode, out var success) is not true ||
            success is not OpenApiResponse response)
        {
            return Task.CompletedTask;
        }

        response.Headers ??= new Dictionary<string, IOpenApiHeader>();

        response.Headers["ETag"] = new OpenApiHeader
        {
            Description =
                "The version of the resource as served. Send it back as If-Match to edit it; "
                + "an edit without one is refused with 428, and one based on a stale version with 412.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Describes an uploaded file inline, and takes the name <c>IFormFile</c> out of the document.
/// </summary>
/// <remarks>
/// <para>
/// Left alone, the generator emits a component named <c>IFormFile</c> holding
/// <c>{ type: string, format: binary }</c> and points every upload at it by reference. Two things
/// are wrong with that. The lesser is that a document meant to be language-neutral names an
/// ASP.NET Core interface. The greater is that generators look for an inline binary schema when
/// deciding whether an operation uploads a file — NSwag produced a client whose method signature
/// took a file and whose supporting class it never emitted, so the generated code did not compile
/// and the reason was three layers from the symptom.
/// </para>
/// <para>
/// So the reference is replaced by what it points at, and the component is dropped.
/// </para>
/// </remarks>
public sealed class UploadedFileTransformer : IOpenApiDocumentTransformer
{
    private const string FormFileSchemaName = "IFormFile";

    /// <summary>
    /// Inlines every reference to the form-file schema and drops the component.
    /// </summary>
    /// <param name="document">The document being emitted.</param>
    /// <param name="context">Unused: the change is decided from the document alone.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Components?.Schemas?.ContainsKey(FormFileSchemaName) is not true)
        {
            return Task.CompletedTask;
        }

        foreach (var operation in document.Paths?.Values.SelectMany(Operations) ?? [])
        {
            InlineFileProperties(operation);
        }

        document.Components.Schemas.Remove(FormFileSchemaName);

        return Task.CompletedTask;
    }

    private static IEnumerable<OpenApiOperation> Operations(IOpenApiPathItem path) =>
        path.Operations is null ? [] : path.Operations.Values;

    private static void InlineFileProperties(OpenApiOperation operation)
    {
        var bodies = operation.RequestBody?.Content?.Values ?? [];

        foreach (var body in bodies)
        {
            if (body.Schema?.Properties is not { } properties)
            {
                continue;
            }

            foreach (var name in properties
                .Where(property => IsFormFileReference(property.Value))
                .Select(property => property.Key)
                .ToArray())
            {
                properties[name] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Format = "binary"
                };
            }
        }
    }

    private static bool IsFormFileReference(IOpenApiSchema schema) =>
        schema is OpenApiSchemaReference reference
        && string.Equals(reference.Reference?.Id, FormFileSchemaName, StringComparison.Ordinal);
}

/// <summary>
/// Declares the JWT bearer scheme on the document.
/// </summary>
/// <remarks>
/// Without it a reference UI has no way to offer a token, so no authenticated endpoint can be tried
/// from it. That was the layered host's situation reversed: it configured the scheme by hand in its
/// own <c>Program.cs</c> while the CQRS host called a bare <c>AddSwaggerGen()</c> and got none.
/// Declaring it here means neither host can be the one that forgets.
/// <para>
/// The scheme is declared and no global requirement is added: which endpoints need a token is
/// already visible from <c>[Authorize]</c>, and the framework reflects it into the document. What a
/// reader could not do was supply the token.
/// </para>
/// </remarks>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] =
            new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the token returned by POST /Auth/login."
            };

        return Task.CompletedTask;
    }
}
