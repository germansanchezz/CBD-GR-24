namespace CBD.Api.Models;

public sealed class DeckCard
{
    public string CardId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public int Quantity { get; set; }
}