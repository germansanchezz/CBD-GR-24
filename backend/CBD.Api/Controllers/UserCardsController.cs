using CBD.Api.Contracts.UserCards;
using CBD.Api.Data;
using CBD.Api.Helpers;
using CBD.Api.Models;
using CBD.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace CBD.Api.Controllers;

[ApiController]
[Route("api/user-cards")]
public sealed class UserCardsController(IMongoClient mongoClient, IOptions<MongoDbOptions> options, IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const int MaxTextFieldLength = 200;
    private const int MaxMainTextLength = 3000;
    private const int MaxSearchTagsCount = 25;
    private const int MaxTagLength = 40;
    private const int MaxQuantityOwned = 999;
    private const int MaxQuantityDelta = 100;
    private const int TopCardsLimit = 3;

    [HttpGet("stats")]
    public async Task<IActionResult> GetUserCardsStats([FromQuery] string? gameType)
    {
        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        await UserCardsDeckUsageSynchronizer.SynchronizeForUserAsync(mongoClient, options.Value, userId);
        await EnrichIncompleteUserCardsAsync(userId, HttpContext.RequestAborted);

        var filter = Builders<UserCard>.Filter.Eq(card => card.UserId, userId);

        if (!string.IsNullOrWhiteSpace(gameType))
        {
            if (!DeckGameTypes.TryNormalize(gameType, out var normalizedGameType))
            {
                return BadRequest(new { message = "gameType debe ser pokemon, magic o yugioh." });
            }

            filter &= Builders<UserCard>.Filter.Eq(card => card.GameType, normalizedGameType);
        }

        var cards = await GetCollection().Find(filter).ToListAsync();

        var totalUniqueCards = cards.Count;
        var totalOwnedCopies = cards.Sum(card => card.QuantityOwned);
        var distinctSets = cards
            .Select(card => card.SetName.Trim())
            .Where(setName => !string.IsNullOrWhiteSpace(setName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var averageCopiesPerCard = totalUniqueCards == 0
            ? 0m
            : decimal.Round((decimal)totalOwnedCopies / totalUniqueCards, 2);

        var gameTypeDistribution = cards
            .GroupBy(card => string.IsNullOrWhiteSpace(card.GameType) ? "unknown" : card.GameType.Trim().ToLowerInvariant())
            .Select(group => new UserCardsByFieldStat(group.Key, group.Sum(card => card.QuantityOwned)))
            .OrderByDescending(stat => stat.TotalOwnedCopies)
            .ThenBy(stat => stat.Label)
            .ToList();

        var rarityDistribution = cards
            .GroupBy(card => string.IsNullOrWhiteSpace(card.Rarity) ? "unknown" : card.Rarity.Trim())
            .Select(group => new UserCardsByFieldStat(group.Key, group.Sum(card => card.QuantityOwned)))
            .OrderByDescending(stat => stat.TotalOwnedCopies)
            .ThenBy(stat => stat.Label)
            .ToList();

        var setDistribution = cards
            .GroupBy(card => string.IsNullOrWhiteSpace(card.SetName) ? "unknown" : card.SetName.Trim())
            .Select(group => new UserCardsByFieldStat(group.Key, group.Sum(card => card.QuantityOwned)))
            .OrderByDescending(stat => stat.TotalOwnedCopies)
            .ThenBy(stat => stat.Label)
            .ToList();

        var topCards = cards
            .OrderByDescending(card => card.QuantityOwned)
            .ThenBy(card => card.Name)
            .Take(TopCardsLimit)
            .Select(card => new UserCardsTopCardStat(card.ExternalCardId, card.Name, card.GameType, card.QuantityOwned))
            .ToList();

        var response = new UserCardsStatsResponse(
            totalUniqueCards,
            totalOwnedCopies,
            distinctSets,
            averageCopiesPerCard,
            gameTypeDistribution,
            rarityDistribution,
            setDistribution,
            topCards);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserCards(
        [FromQuery] string? gameType,
        [FromQuery] string? name,
        [FromQuery] string? rarity,
        [FromQuery] string? setName,
        [FromQuery] bool inUseOnly = false,
        [FromQuery] int? minQuantityOwned = null,
        [FromQuery] int? maxQuantityOwned = null)
    {
        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        await UserCardsDeckUsageSynchronizer.SynchronizeForUserAsync(mongoClient, options.Value, userId);
        await EnrichIncompleteUserCardsAsync(userId, HttpContext.RequestAborted);

        if (minQuantityOwned is < 0 || maxQuantityOwned is < 0)
        {
            return BadRequest(new { message = "minQuantityOwned y maxQuantityOwned no pueden ser negativos." });
        }

        if (minQuantityOwned.HasValue && maxQuantityOwned.HasValue && minQuantityOwned > maxQuantityOwned)
        {
            return BadRequest(new { message = "minQuantityOwned no puede ser mayor que maxQuantityOwned." });
        }

        var filter = Builders<UserCard>.Filter.Eq(card => card.UserId, userId);

        if (!string.IsNullOrWhiteSpace(gameType))
        {
            if (!DeckGameTypes.TryNormalize(gameType, out var normalizedGameType))
            {
                return BadRequest(new { message = "gameType debe ser pokemon, magic o yugioh." });
            }

            filter &= Builders<UserCard>.Filter.Eq(card => card.GameType, normalizedGameType);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var safeName = Regex.Escape(name.Trim());
            filter &= Builders<UserCard>.Filter.Regex(card => card.Name, new BsonRegularExpression(safeName, "i"));
        }

        if (!string.IsNullOrWhiteSpace(rarity))
        {
            var safeRarity = Regex.Escape(rarity.Trim());
            filter &= Builders<UserCard>.Filter.Regex(card => card.Rarity, new BsonRegularExpression(safeRarity, "i"));
        }

        if (!string.IsNullOrWhiteSpace(setName))
        {
            var safeSetName = Regex.Escape(setName.Trim());
            filter &= Builders<UserCard>.Filter.Regex(card => card.SetName, new BsonRegularExpression(safeSetName, "i"));
        }

        if (inUseOnly)
        {
            filter &= Builders<UserCard>.Filter.Gt(card => card.QuantityInDecks, 0);
        }

        if (minQuantityOwned.HasValue)
        {
            filter &= Builders<UserCard>.Filter.Gte(card => card.QuantityOwned, minQuantityOwned.Value);
        }

        if (maxQuantityOwned.HasValue)
        {
            filter &= Builders<UserCard>.Filter.Lte(card => card.QuantityOwned, maxQuantityOwned.Value);
        }

        var cards = await GetCollection()
            .Find(filter)
            .SortByDescending(card => card.UpdatedAtUtc)
            .ToListAsync();

        return Ok(cards);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertUserCard([FromBody] UpsertUserCardRequest request, CancellationToken cancellationToken)
    {
        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        if (!DeckGameTypes.TryNormalize(request.GameType, out var normalizedGameType))
        {
            return BadRequest(new { message = "gameType debe ser pokemon, magic o yugioh." });
        }

        if (string.IsNullOrWhiteSpace(request.ExternalCardId) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return BadRequest(new { message = "externalCardId, name e imageUrl son obligatorios." });
        }

        if (request.QuantityOwned <= 0)
        {
            return BadRequest(new { message = "quantityOwned debe ser mayor que 0." });
        }

        if (request.QuantityOwned > MaxQuantityOwned)
        {
            return BadRequest(new { message = $"quantityOwned no puede superar {MaxQuantityOwned}." });
        }

        if (request.ExternalCardId.Trim().Length > MaxTextFieldLength ||
            request.Name.Trim().Length > MaxTextFieldLength ||
            request.ImageUrl.Trim().Length > 1000 ||
            (request.SetName?.Trim().Length ?? 0) > MaxTextFieldLength ||
            (request.Rarity?.Trim().Length ?? 0) > MaxTextFieldLength ||
            (request.TypeLine?.Trim().Length ?? 0) > MaxTextFieldLength)
        {
            return BadRequest(new { message = "Uno o varios campos de texto superan el tamano permitido." });
        }

        if ((request.MainText?.Length ?? 0) > MaxMainTextLength)
        {
            return BadRequest(new { message = $"mainText no puede superar {MaxMainTextLength} caracteres." });
        }

        if (request.SearchTags is not null)
        {
            if (request.SearchTags.Count > MaxSearchTagsCount)
            {
                return BadRequest(new { message = $"searchTags no puede superar {MaxSearchTagsCount} elementos." });
            }

            if (request.SearchTags.Any(tag => (tag?.Trim().Length ?? 0) > MaxTagLength))
            {
                return BadRequest(new { message = $"Cada tag no puede superar {MaxTagLength} caracteres." });
            }
        }

        var requestTags = NormalizeTags(request.SearchTags);
        var externalCardId = request.ExternalCardId.Trim();

        var shouldEnrich = string.IsNullOrWhiteSpace(request.SetName)
            || string.IsNullOrWhiteSpace(request.Rarity)
            || string.IsNullOrWhiteSpace(request.TypeLine)
            || string.IsNullOrWhiteSpace(request.MainText)
            || requestTags.Count == 0;

        var enriched = shouldEnrich
            ? await TryGetProviderEnrichmentAsync(normalizedGameType, externalCardId, cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        var collection = GetCollection();

        var setName = Clip(PickFirstNonEmpty(request.SetName, enriched?.SetName), MaxTextFieldLength);
        var rarity = Clip(PickFirstNonEmpty(request.Rarity, enriched?.Rarity), MaxTextFieldLength);
        var typeLine = Clip(PickFirstNonEmpty(request.TypeLine, enriched?.TypeLine), MaxTextFieldLength);
        var mainText = Clip(PickFirstNonEmpty(request.MainText, enriched?.MainText), MaxMainTextLength);
        var tags = MergeTags(requestTags, enriched?.SearchTags);

        var existingCard = await collection
            .Find(card => card.UserId == userId && card.GameType == normalizedGameType && card.ExternalCardId == externalCardId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingCard is null)
        {
            var newCard = new UserCard
            {
                UserId = userId,
                GameType = normalizedGameType,
                ExternalCardId = externalCardId,
                Name = request.Name.Trim(),
                ImageUrl = request.ImageUrl.Trim(),
                SetName = setName,
                Rarity = rarity,
                TypeLine = typeLine,
                SearchTags = tags,
                MainText = mainText,
                QuantityOwned = request.QuantityOwned,
                QuantityInDecks = 0,
                AddedAtUtc = now,
                UpdatedAtUtc = now
            };

            await collection.InsertOneAsync(newCard, cancellationToken: cancellationToken);
            return Created($"/api/user-cards/{newCard.Id}", newCard);
        }

        existingCard.Name = request.Name.Trim();
        existingCard.ImageUrl = request.ImageUrl.Trim();
        existingCard.SetName = setName;
        existingCard.Rarity = rarity;
        existingCard.TypeLine = typeLine;
        existingCard.SearchTags = tags;
        existingCard.MainText = mainText;
        existingCard.QuantityOwned += request.QuantityOwned;
        existingCard.UpdatedAtUtc = now;

        await collection.ReplaceOneAsync(card => card.Id == existingCard.Id, existingCard, cancellationToken: cancellationToken);

        return Ok(existingCard);
    }

    [HttpDelete("{userCardId}")]
    public async Task<IActionResult> DeleteUserCard(string userCardId)
    {
        if (!ObjectId.TryParse(userCardId, out _))
        {
            return BadRequest(new { message = "userCardId no es valido." });
        }

        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        var result = await GetCollection()
            .DeleteOneAsync(card => card.Id == userCardId && card.UserId == userId);

        return result.DeletedCount == 0
            ? NotFound(new { message = "Carta no encontrada en la coleccion." })
            : NoContent();
    }

    [HttpPost("{userCardId}/quantity")]
    public async Task<IActionResult> AdjustUserCardQuantity(string userCardId, [FromBody] AdjustUserCardQuantityRequest request)
    {
        if (!ObjectId.TryParse(userCardId, out _))
        {
            return BadRequest(new { message = "userCardId no es valido." });
        }

        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        if (request.Delta == 0)
        {
            return BadRequest(new { message = "delta debe ser distinto de 0." });
        }

        if (Math.Abs(request.Delta) > MaxQuantityDelta)
        {
            return BadRequest(new { message = $"delta no puede superar {MaxQuantityDelta} en valor absoluto." });
        }

        var collection = GetCollection();
        var card = await collection
            .Find(existingCard => existingCard.Id == userCardId && existingCard.UserId == userId)
            .FirstOrDefaultAsync();

        if (card is null)
        {
            return NotFound(new { message = "Carta no encontrada en la coleccion." });
        }

        var nextQuantityOwned = card.QuantityOwned + request.Delta;
        if (nextQuantityOwned < 0)
        {
            return BadRequest(new { message = "No puedes dejar quantityOwned en negativo." });
        }

        if (nextQuantityOwned > MaxQuantityOwned)
        {
            return BadRequest(new { message = $"quantityOwned no puede superar {MaxQuantityOwned}." });
        }

        card.QuantityOwned = nextQuantityOwned;
        card.UpdatedAtUtc = DateTime.UtcNow;

        await collection.ReplaceOneAsync(existingCard => existingCard.Id == card.Id, card);

        return Ok(card);
    }

    private IMongoCollection<UserCard> GetCollection()
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        return database.GetCollection<UserCard>(options.Value.UserCardsCollectionName);
    }

    private static List<string> NormalizeTags(IEnumerable<string?>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!.Trim().ToLowerInvariant())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task EnrichIncompleteUserCardsAsync(string userId, CancellationToken cancellationToken)
    {
        var collection = GetCollection();
        var cards = await collection
            .Find(card => card.UserId == userId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var writes = new List<WriteModel<UserCard>>();

        foreach (var card in cards)
        {
            if (string.IsNullOrWhiteSpace(card.Id)
                || string.IsNullOrWhiteSpace(card.ExternalCardId)
                || string.IsNullOrWhiteSpace(card.GameType))
            {
                continue;
            }

            if (!NeedsEnrichment(card))
            {
                continue;
            }

            var enriched = await TryGetProviderEnrichmentAsync(card.GameType, card.ExternalCardId, cancellationToken);
            if (enriched is null)
            {
                continue;
            }

            var mergedTags = MergeTags(card.SearchTags, enriched.SearchTags);
            var setName = Clip(PickFirstNonEmpty(card.SetName, enriched.SetName), MaxTextFieldLength);
            var rarity = Clip(PickFirstNonEmpty(card.Rarity, enriched.Rarity), MaxTextFieldLength);
            var typeLine = Clip(PickFirstNonEmpty(card.TypeLine, enriched.TypeLine), MaxTextFieldLength);
            var mainText = Clip(PickFirstNonEmpty(card.MainText, enriched.MainText), MaxMainTextLength);

            var update = Builders<UserCard>.Update
                .Set(existing => existing.SetName, setName)
                .Set(existing => existing.Rarity, rarity)
                .Set(existing => existing.TypeLine, typeLine)
                .Set(existing => existing.MainText, mainText)
                .Set(existing => existing.SearchTags, mergedTags)
                .Set(existing => existing.UpdatedAtUtc, now);

            writes.Add(new UpdateOneModel<UserCard>(Builders<UserCard>.Filter.Eq(existing => existing.Id, card.Id), update));
        }

        if (writes.Count > 0)
        {
            await collection.BulkWriteAsync(writes, cancellationToken: cancellationToken);
        }
    }

    private static bool NeedsEnrichment(UserCard card)
    {
        return string.IsNullOrWhiteSpace(card.SetName)
            || string.IsNullOrWhiteSpace(card.Rarity)
            || string.IsNullOrWhiteSpace(card.TypeLine)
            || string.IsNullOrWhiteSpace(card.MainText)
            || card.SearchTags.Count == 0;
    }

    private async Task<ProviderCardEnrichment?> TryGetProviderEnrichmentAsync(string gameType, string externalCardId, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("external-cards");

        return gameType switch
        {
            DeckGameTypes.Pokemon => await TryGetPokemonEnrichmentAsync(httpClient, externalCardId, cancellationToken),
            DeckGameTypes.Magic => await TryGetMagicEnrichmentAsync(httpClient, externalCardId, cancellationToken),
            DeckGameTypes.Yugioh => await TryGetYugiohEnrichmentAsync(httpClient, externalCardId, cancellationToken),
            _ => null,
        };
    }

    private static async Task<ProviderCardEnrichment?> TryGetPokemonEnrichmentAsync(HttpClient httpClient, string externalCardId, CancellationToken cancellationToken)
    {
        var encodedId = Uri.EscapeDataString(externalCardId);
        using var response = await httpClient.GetAsync($"https://api.tcgdex.net/v2/es/cards/{encodedId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            return null;
        }

        var root = payload.RootElement;
        var categories = GetStringArray(root, "types");

        return new ProviderCardEnrichment
        {
            SetName = GetNestedString(root, "set", "name"),
            Rarity = GetString(root, "rarity"),
            TypeLine = PickFirstNonEmpty(GetString(root, "category"), string.Join('/', categories)),
            MainText = PickFirstNonEmpty(GetString(root, "effect"), GetString(root, "description")),
            SearchTags = categories
        };
    }

    private static async Task<ProviderCardEnrichment?> TryGetMagicEnrichmentAsync(HttpClient httpClient, string externalCardId, CancellationToken cancellationToken)
    {
        var encodedId = Uri.EscapeDataString(externalCardId);
        using var response = await httpClient.GetAsync($"https://api.scryfall.com/cards/{encodedId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            return null;
        }

        var root = payload.RootElement;
        var colors = GetStringArray(root, "colors");

        return new ProviderCardEnrichment
        {
            SetName = GetString(root, "set_name"),
            Rarity = GetString(root, "rarity"),
            TypeLine = GetString(root, "type_line"),
            MainText = PickFirstNonEmpty(GetString(root, "oracle_text"), GetString(root, "flavor_text")),
            SearchTags = colors
        };
    }

    private static async Task<ProviderCardEnrichment?> TryGetYugiohEnrichmentAsync(HttpClient httpClient, string externalCardId, CancellationToken cancellationToken)
    {
        var encodedId = Uri.EscapeDataString(externalCardId);
        using var response = await httpClient.GetAsync($"https://db.ygoprodeck.com/api/v7/cardinfo.php?id={encodedId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            return null;
        }

        if (!payload.RootElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array || dataElement.GetArrayLength() == 0)
        {
            return null;
        }

        var root = dataElement[0];
        var type = GetString(root, "type");
        var race = GetString(root, "race");

        return new ProviderCardEnrichment
        {
            SetName = GetNestedString(root, "card_sets", 0, "set_name"),
            Rarity = GetNestedString(root, "card_sets", 0, "set_rarity"),
            TypeLine = PickFirstNonEmpty(type, race),
            MainText = GetString(root, "desc"),
            SearchTags = NormalizeTags([type, race, GetString(root, "archetype")])
        };
    }

    private static List<string> MergeTags(List<string> requestTags, List<string>? enrichedTags)
    {
        if (enrichedTags is null || enrichedTags.Count == 0)
        {
            return requestTags;
        }

        return requestTags
            .Concat(enrichedTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxSearchTagsCount)
            .ToList();
    }

    private static string Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? PickFirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null,
        };
    }

    private static List<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxSearchTagsCount)
            .ToList();
    }

    private static string? GetNestedString(JsonElement element, string propertyName, string childPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(property, childPropertyName);
    }

    private static string? GetNestedString(JsonElement element, string arrayPropertyName, int index, string childPropertyName)
    {
        if (!element.TryGetProperty(arrayPropertyName, out var property) || property.ValueKind != JsonValueKind.Array || property.GetArrayLength() <= index)
        {
            return null;
        }

        var child = property[index];
        if (child.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(child, childPropertyName);
    }

    private sealed class ProviderCardEnrichment
    {
        public string? SetName { get; set; }

        public string? Rarity { get; set; }

        public string? TypeLine { get; set; }

        public string? MainText { get; set; }

        public List<string> SearchTags { get; set; } = [];
    }
}
