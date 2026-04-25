using CBD.Api.Contracts.UserCards;
using CBD.Api.Helpers;
using CBD.Api.Models;
using CBD.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace CBD.Api.Controllers;

[ApiController]
[Route("api/user-cards")]
public sealed class UserCardsController(IMongoClient mongoClient, IOptions<MongoDbOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUserCards(
        [FromQuery] string? gameType,
        [FromQuery] string? name,
        [FromQuery] string? rarity,
        [FromQuery] string? setName)
    {
        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
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

        var cards = await GetCollection()
            .Find(filter)
            .SortByDescending(card => card.UpdatedAtUtc)
            .ToListAsync();

        return Ok(cards);
    }

    [HttpPost]
    public async Task<IActionResult> UpsertUserCard([FromBody] UpsertUserCardRequest request)
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

        var now = DateTime.UtcNow;
        var collection = GetCollection();
        var externalCardId = request.ExternalCardId.Trim();

        var existingCard = await collection
            .Find(card => card.UserId == userId && card.GameType == normalizedGameType && card.ExternalCardId == externalCardId)
            .FirstOrDefaultAsync();

        if (existingCard is null)
        {
            var newCard = new UserCard
            {
                UserId = userId,
                GameType = normalizedGameType,
                ExternalCardId = externalCardId,
                Name = request.Name.Trim(),
                ImageUrl = request.ImageUrl.Trim(),
                SetName = request.SetName?.Trim() ?? string.Empty,
                Rarity = request.Rarity?.Trim() ?? string.Empty,
                TypeLine = request.TypeLine?.Trim() ?? string.Empty,
                SearchTags = NormalizeTags(request.SearchTags),
                MainText = request.MainText?.Trim() ?? string.Empty,
                Stats = MapStats(request.Stats),
                QuantityOwned = request.QuantityOwned,
                QuantityInDecks = 0,
                AddedAtUtc = now,
                UpdatedAtUtc = now
            };

            await collection.InsertOneAsync(newCard);
            return Created($"/api/user-cards/{newCard.Id}", newCard);
        }

        existingCard.Name = request.Name.Trim();
        existingCard.ImageUrl = request.ImageUrl.Trim();
        existingCard.SetName = request.SetName?.Trim() ?? string.Empty;
        existingCard.Rarity = request.Rarity?.Trim() ?? string.Empty;
        existingCard.TypeLine = request.TypeLine?.Trim() ?? string.Empty;
        existingCard.SearchTags = NormalizeTags(request.SearchTags);
        existingCard.MainText = request.MainText?.Trim() ?? string.Empty;
        existingCard.Stats = MapStats(request.Stats);
        existingCard.QuantityOwned += request.QuantityOwned;
        existingCard.UpdatedAtUtc = now;

        await collection.ReplaceOneAsync(card => card.Id == existingCard.Id, existingCard);

        return Ok(existingCard);
    }

    private IMongoCollection<UserCard> GetCollection()
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        return database.GetCollection<UserCard>(options.Value.UserCardsCollectionName);
    }

    private static UserCardStats MapStats(UserCardStatsRequest? stats)
    {
        if (stats is null)
        {
            return new UserCardStats();
        }

        return new UserCardStats
        {
            Attack = stats.Attack,
            Defense = stats.Defense,
            Hp = stats.Hp,
            Cost = stats.Cost,
            Level = stats.Level,
            Colors = NormalizeTags(stats.Colors),
            Attribute = stats.Attribute?.Trim() ?? string.Empty
        };
    }

    private static List<string> NormalizeTags(List<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        return tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
