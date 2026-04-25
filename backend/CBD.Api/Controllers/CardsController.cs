using CBD.Api.Contracts.Cards;
using CBD.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CBD.Api.Controllers;

[ApiController]
[Route("api/cards")]
public sealed class CardsController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string gameType, [FromQuery] string name, CancellationToken cancellationToken)
    {
        if (!DeckGameTypes.TryNormalize(gameType, out var normalizedGameType))
        {
            return BadRequest(new { message = "gameType debe ser pokemon, magic o yugioh." });
        }

        var query = name.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { message = "name es obligatorio." });
        }

        var httpClient = httpClientFactory.CreateClient("external-cards");

        try
        {
            var cards = normalizedGameType switch
            {
                DeckGameTypes.Pokemon => await SearchPokemonCardsAsync(httpClient, query, cancellationToken),
                DeckGameTypes.Magic => await SearchMagicCardsAsync(httpClient, query, cancellationToken),
                DeckGameTypes.Yugioh => await SearchYugiohCardsAsync(httpClient, query, cancellationToken),
                _ => []
            };

            return Ok(cards);
        }
        catch (HttpRequestException exception)
        {
            return Problem(title: "Card provider request failed", detail: exception.Message, statusCode: 503);
        }
    }

    private static async Task<List<CardSearchResultResponse>> SearchPokemonCardsAsync(HttpClient httpClient, string name, CancellationToken cancellationToken)
    {
        var encodedName = Uri.EscapeDataString(name);
        var cards = await httpClient.GetFromJsonAsync<List<TcgDexCardSummary>>($"https://api.tcgdex.net/v2/es/cards?name={encodedName}", cancellationToken)
            ?? [];

        var results = new List<CardSearchResultResponse>();

        foreach (var card in cards.Take(50))
        {
            if (string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(card.Name))
            {
                continue;
            }

            var details = await httpClient.GetFromJsonAsync<TcgDexCardDetail>($"https://api.tcgdex.net/v2/es/cards/{card.Id}", cancellationToken);
            var imageBase = card.Image ?? details?.Image;

            if (string.IsNullOrWhiteSpace(imageBase))
            {
                continue;
            }

            results.Add(new CardSearchResultResponse(card.Id, card.Name, $"{imageBase}/high.webp", BuildPokemonProperties(details)));
        }

        return results;
    }

    private static async Task<List<CardSearchResultResponse>> SearchMagicCardsAsync(HttpClient httpClient, string name, CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString($"name:{name} lang:es");
        using var response = await httpClient.GetAsync($"https://api.scryfall.com/cards/search?q={encodedQuery}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Scryfall devolvio {(int)response.StatusCode} al buscar cartas de Magic.");
        }

        var searchResponse = await response.Content.ReadFromJsonAsync<ScryfallSearchResponse>(cancellationToken: cancellationToken);
        if (searchResponse?.Data is null)
        {
            return [];
        }

        return searchResponse.Data
            .Take(50)
            .Select(card =>
            {
                var imageUrl = card.ImageUris?.Normal
                    ?? card.CardFaces?.FirstOrDefault(face => !string.IsNullOrWhiteSpace(face.ImageUris?.Normal))?.ImageUris?.Normal;

                if (string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(imageUrl))
                {
                    return null;
                }

                var displayName = string.IsNullOrWhiteSpace(card.PrintedName) ? card.Name : card.PrintedName;
                return new CardSearchResultResponse(card.Id, displayName, imageUrl, BuildMagicProperties(card));
            })
            .Where(card => card is not null)
            .Cast<CardSearchResultResponse>()
            .ToList();
    }

    private static async Task<List<CardSearchResultResponse>> SearchYugiohCardsAsync(HttpClient httpClient, string name, CancellationToken cancellationToken)
    {
        var encodedName = Uri.EscapeDataString(name);
        var response = await httpClient.GetFromJsonAsync<YugiohSearchResponse>($"https://db.ygoprodeck.com/api/v7/cardinfo.php?fname={encodedName}", cancellationToken);
        if (response?.Data is null)
        {
            return [];
        }

        return response.Data
            .Take(50)
            .Select(card =>
            {
                var imageUrl = card.CardImages?.FirstOrDefault()?.ImageUrl;
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    return null;
                }

                return new CardSearchResultResponse(card.Id.ToString(), card.Name, imageUrl, BuildYugiohProperties(card));
            })
            .Where(card => card is not null)
            .Cast<CardSearchResultResponse>()
            .ToList();
    }

    private static List<string> BuildPokemonProperties(TcgDexCardDetail? details)
    {
        var properties = new List<string>();

        if (!string.IsNullOrWhiteSpace(details?.Category))
        {
            properties.Add(details.Category.Trim());
        }

        if (details?.Types is not null)
        {
            properties.AddRange(details.Types.Where(type => !string.IsNullOrWhiteSpace(type)).Select(type => type.Trim()));
        }

        return NormalizeProperties(properties);
    }

    private static List<string> BuildMagicProperties(ScryfallCard card)
    {
        var typeLine = string.IsNullOrWhiteSpace(card.PrintedTypeLine) ? card.TypeLine : card.PrintedTypeLine;
        var properties = new List<string>();

        if (!string.IsNullOrWhiteSpace(typeLine))
        {
            var separator = typeLine.Contains('—') ? '—' : typeLine.Contains('-') ? '-' : '\0';
            var mainType = separator == '\0'
                ? typeLine.Trim()
                : typeLine.Split(separator, 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

            properties.Add(mainType);
        }

        if (card.Colors is null || card.Colors.Count == 0)
        {
            properties.Add("Incolora");
        }
        else
        {
            properties.AddRange(card.Colors.Select(MapMagicColor));
        }

        return NormalizeProperties(properties);
    }

    private static string MapMagicColor(string color)
    {
        return color.Trim().ToUpperInvariant() switch
        {
            "W" => "Blanco",
            "U" => "Azul",
            "B" => "Negro",
            "R" => "Rojo",
            "G" => "Verde",
            _ => color.Trim(),
        };
    }

    private static List<string> BuildYugiohProperties(YugiohCard card)
    {
        var properties = new List<string>();

        if (!string.IsNullOrWhiteSpace(card.Type))
        {
            properties.Add(card.Type.Trim());
        }

        if (card.Type.Contains("Monster", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(card.Attribute))
        {
            properties.Add(card.Attribute.Trim());
        }

        return NormalizeProperties(properties);
    }

    private static List<string> NormalizeProperties(IEnumerable<string> properties)
    {
        return properties
            .Where(property => !string.IsNullOrWhiteSpace(property))
            .Select(property => property.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class TcgDexCardSummary
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public string? Image { get; set; }
    }

    private sealed class TcgDexCardDetail
    {
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("types")]
        public List<string>? Types { get; set; }
    }

    private sealed class ScryfallSearchResponse
    {
        [JsonPropertyName("data")]
        public List<ScryfallCard>? Data { get; set; }
    }

    private sealed class ScryfallCard
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("printed_name")]
        public string? PrintedName { get; set; }

        [JsonPropertyName("image_uris")]
        public ScryfallImageUris? ImageUris { get; set; }

        [JsonPropertyName("colors")]
        public List<string>? Colors { get; set; }

        [JsonPropertyName("type_line")]
        public string? TypeLine { get; set; }

        [JsonPropertyName("printed_type_line")]
        public string? PrintedTypeLine { get; set; }

        [JsonPropertyName("card_faces")]
        public List<ScryfallCardFace>? CardFaces { get; set; }
    }

    private sealed class ScryfallCardFace
    {
        [JsonPropertyName("image_uris")]
        public ScryfallImageUris? ImageUris { get; set; }
    }

    private sealed class ScryfallImageUris
    {
        [JsonPropertyName("normal")]
        public string? Normal { get; set; }
    }

    private sealed class YugiohSearchResponse
    {
        [JsonPropertyName("data")]
        public List<YugiohCard>? Data { get; set; }
    }

    private sealed class YugiohCard
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("attribute")]
        public string? Attribute { get; set; }

        [JsonPropertyName("card_images")]
        public List<YugiohCardImage>? CardImages { get; set; }
    }

    private sealed class YugiohCardImage
    {
        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }
    }
}
