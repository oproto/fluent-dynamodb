using System.Text.Json;

namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Represents a continuation token for paginated spatial queries.
/// Contains the current cell index and DynamoDB's LastEvaluatedKey for resuming queries.
/// </summary>
public class SpatialContinuationToken
{
    /// <summary>
    /// Gets or sets the index of the current cell in the spiral-ordered cell list.
    /// </summary>
    public int CellIndex { get; set; }

    /// <summary>
    /// Gets or sets DynamoDB's LastEvaluatedKey for pagination within the current cell.
    /// Null if the cell is exhausted and we should move to the next cell.
    /// </summary>
    public string? LastEvaluatedKey { get; set; }

    /// <summary>
    /// Serializes the continuation token to a Base64-encoded string.
    /// </summary>
    /// <returns>A Base64-encoded string representation of the token.</returns>
    /// <remarks>
    /// Uses System.Text.Json for AOT compatibility.
    /// The token can be passed between requests to resume pagination.
    /// </remarks>
    public string ToBase64()
    {
        var json = JsonSerializer.Serialize(this);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Deserializes a continuation token from a Base64-encoded string.
    /// </summary>
    /// <param name="token">The Base64-encoded token string.</param>
    /// <returns>A <see cref="SpatialContinuationToken"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when token is null.</exception>
    /// <exception cref="FormatException">Thrown when token is not valid Base64.</exception>
    /// <exception cref="JsonException">Thrown when token contains invalid JSON.</exception>
    /// <remarks>
    /// Uses System.Text.Json for AOT compatibility.
    /// </remarks>
    public static SpatialContinuationToken FromBase64(string token)
    {
        if (token == null)
            throw new ArgumentNullException(nameof(token));

        var bytes = Convert.FromBase64String(token);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<SpatialContinuationToken>(json)
            ?? throw new JsonException("Failed to deserialize continuation token");
    }
}
