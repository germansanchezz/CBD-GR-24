namespace CBD.Api.Contracts.Decks;

public sealed record DeckCardRequest(string CardId, string Name, string ImageUrl, int Quantity, List<string>? Properties = null);