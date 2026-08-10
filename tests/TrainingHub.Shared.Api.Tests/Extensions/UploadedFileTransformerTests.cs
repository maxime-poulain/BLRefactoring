using TrainingHub.Shared.Api.Extensions;
using AwesomeAssertions;
using Microsoft.OpenApi;
using Xunit;

namespace TrainingHub.Shared.Api.Tests.Extensions;

/// <summary>
/// Behavior covered for the transformer that describes an uploaded file inline.
/// </summary>
/// <remarks>
/// Worth a test rather than a glance, because the failure it prevents is silent and lands three
/// layers away. The document generator names the binary schema <c>IFormFile</c> and points uploads
/// at it by reference; NSwag decides an operation takes a file by looking for an <em>inline</em>
/// binary schema, so it generated a method whose parameter type it never emitted, and the build
/// that broke was the client's rather than the API's.
/// </remarks>
public sealed class UploadedFileTransformerTests
{
    /// <summary>
    /// Transform, form file reference, is replaced by the binary schema it pointed at.
    /// </summary>
    [Fact]
    public async Task Transform_FormFileReference_IsReplacedByTheBinarySchema()
    {
        // Arrange
        var document = DocumentWithAnUpload();

        // Act
        await new UploadedFileTransformer().TransformAsync(document, null!, CancellationToken.None);

        // Assert
        var property = document.Paths["/Trainer/me/photo"]
            .Operations![HttpMethod.Put]
            .RequestBody!
            .Content!["multipart/form-data"]
            .Schema!
            .Properties!["Photo"];

        property.Should().BeOfType<OpenApiSchema>();
        property.Type.Should().Be(JsonSchemaType.String);
        property.Format.Should().Be("binary");
    }

    /// <summary>
    /// Transform, form file reference, takes the framework's type name out of the document.
    /// </summary>
    /// <remarks>
    /// The lesser half of the same change, and still worth keeping true: a document meant to be
    /// read by generators in other languages should not name an ASP.NET Core interface.
    /// </remarks>
    [Fact]
    public async Task Transform_FormFileReference_TakesTheTypeNameOutOfTheDocument()
    {
        // Arrange
        var document = DocumentWithAnUpload();

        // Act
        await new UploadedFileTransformer().TransformAsync(document, null!, CancellationToken.None);

        // Assert
        document.Components!.Schemas.Should().NotContainKey("IFormFile");
    }

    /// <summary>
    /// Transform, document with no upload, changes nothing.
    /// </summary>
    [Fact]
    public async Task Transform_DocumentWithNoUpload_ChangesNothing()
    {
        // Arrange
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["TrainerHttpResponse"] = new OpenApiSchema { Type = JsonSchemaType.Object }
                }
            }
        };

        // Act
        await new UploadedFileTransformer().TransformAsync(document, null!, CancellationToken.None);

        // Assert
        document.Components.Schemas.Should().ContainKey("TrainerHttpResponse");
    }

    private static OpenApiDocument DocumentWithAnUpload()
    {
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["IFormFile"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
                }
            }
        };

        document.AddComponent("IFormFile", document.Components.Schemas["IFormFile"]);

        document.Paths = new OpenApiPaths
        {
            ["/Trainer/me/photo"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Put] = new OpenApiOperation
                    {
                        RequestBody = new OpenApiRequestBody
                        {
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["multipart/form-data"] = new OpenApiMediaType
                                {
                                    Schema = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.Object,
                                        Properties = new Dictionary<string, IOpenApiSchema>
                                        {
                                            ["Photo"] = new OpenApiSchemaReference("IFormFile", document)
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return document;
    }
}
