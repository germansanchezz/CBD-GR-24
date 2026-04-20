using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CBD.Api.Models;

public sealed class Deck
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string OwnerUserId { get; set; } = string.Empty;

    public List<DeckCard> Cards { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
