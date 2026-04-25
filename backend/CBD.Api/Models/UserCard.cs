using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CBD.Api.Models;

[BsonIgnoreExtraElements]
public sealed class UserCard
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    public string GameType { get; set; } = DeckGameTypes.Pokemon;

    public string ExternalCardId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public string SetName { get; set; } = string.Empty;

    public string Rarity { get; set; } = string.Empty;

    public string TypeLine { get; set; } = string.Empty;

    public List<string> SearchTags { get; set; } = [];

    public string MainText { get; set; } = string.Empty;

    public int QuantityOwned { get; set; }

    public int QuantityInDecks { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
