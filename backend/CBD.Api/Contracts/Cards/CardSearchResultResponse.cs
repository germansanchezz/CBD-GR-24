namespace CBD.Api.Contracts.Cards;

public sealed record CardSearchResultResponse(string CardId, string Name, string ImageUrl, List<string> Properties);
