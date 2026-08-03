namespace TrainingHub.Shared.Api.Http;

/// <summary>
/// Converts an aggregate's row version to and from an HTTP entity tag.
/// </summary>
/// <remarks>
/// The version travels as an <c>ETag</c> on reads and comes back as <c>If-Match</c>
/// on writes, which is what HTTP already provides for exactly this purpose. Keeping
/// it in headers leaves the business payloads free of a technical field.
/// </remarks>
public static class EntityTag
{
    /// <summary>
    /// Formats a row version as a strong entity tag, quotes included.
    /// </summary>
    public static string From(byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);

        return $"\"{Convert.ToBase64String(rowVersion)}\"";
    }

    /// <summary>
    /// Reads back a row version from an <c>If-Match</c> value.
    /// </summary>
    /// <remarks>
    /// Only a strong, single entity tag is accepted. A weak validator (<c>W/</c>)
    /// or the wildcard <c>*</c> is rejected: both would let a caller through
    /// without stating which version they actually read, which is the whole point
    /// of the check.
    /// </remarks>
    public static bool TryParse(string? entityTag, out byte[] rowVersion)
    {
        rowVersion = [];

        if (string.IsNullOrWhiteSpace(entityTag))
        {
            return false;
        }

        var trimmed = entityTag.Trim();
        if (trimmed.Length < 3 || trimmed[0] != '"' || trimmed[^1] != '"')
        {
            return false;
        }

        return TryDecode(trimmed[1..^1], out rowVersion);
    }

    private static bool TryDecode(string base64, out byte[] rowVersion)
    {
        try
        {
            rowVersion = Convert.FromBase64String(base64);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }
}
