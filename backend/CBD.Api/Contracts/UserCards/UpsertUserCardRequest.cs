namespace CBD.Api.Contracts.UserCards;

public sealed record UpsertUserCardRequest(
    string GameType,
    string ExternalCardId,
    string Name,
    string ImageUrl,
    string? SetName,
    string? Rarity,
    string? TypeLine,
    List<string>? SearchTags,
    string? MainText,
    UserCardStatsRequest? Stats,
    int QuantityOwned = 1);

public sealed record UserCardStatsRequest(
    int? Hp,
    List<string>? Colors,
    string? Attribute);
