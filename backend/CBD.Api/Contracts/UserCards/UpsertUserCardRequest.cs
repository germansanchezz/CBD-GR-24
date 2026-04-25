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
    int QuantityOwned = 1);
