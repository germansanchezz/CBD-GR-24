namespace CBD.Api.Models;

public sealed class DeckCard
{
    public string CardId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public List<string> Properties { get; set; } = [];

    public int Quantity { get; set; }
}