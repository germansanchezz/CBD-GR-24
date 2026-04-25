using CBD.Api.Contracts.Decks;
using CBD.Api.Helpers;
using CBD.Api.Models;
using CBD.Api.Options;
using CBD.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CBD.Api.Controllers;

[ApiController]
[Route("api/decks")]
public sealed class DecksController(IMongoClient mongoClient, IOptions<MongoDbOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDecks()
    {
        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        var decksCollection = GetDecksCollection();
        var filter = Builders<Deck>.Filter.Eq(deck => deck.OwnerUserId, userId);

        var decks = await decksCollection
            .Find(filter)
            .SortByDescending(deck => deck.UpdatedAtUtc)
            .ToListAsync();

        return Ok(decks);
    }

    [HttpGet("{deckId}")]
    public async Task<IActionResult> GetDeckById(string deckId)
    {
        if (!ObjectId.TryParse(deckId, out _))
        {
            return BadRequest(new { message = "deckId no es valido." });
        }

        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        var decksCollection = GetDecksCollection();

        var filter = Builders<Deck>.Filter.Eq(deck => deck.Id, deckId)
            & Builders<Deck>.Filter.Eq(deck => deck.OwnerUserId, userId);

        var deck = await decksCollection.Find(filter).FirstOrDefaultAsync();

        return deck is null
            ? NotFound(new { message = "Baraja no encontrada." })
            : Ok(deck);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDeck([FromBody] CreateDeckRequest request)
    {
        if (!DeckRequestValidator.TryValidateCreate(request, out var createErrorMessage))
        {
            return BadRequest(new { message = createErrorMessage });
        }

        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        DeckGameTypes.TryNormalize(request.GameType, out var gameType);

        var deck = new Deck
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            GameType = gameType,
            OwnerUserId = userId,
            Cards = [],
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var decksCollection = GetDecksCollection();
        await decksCollection.InsertOneAsync(deck);

        return Created($"/api/decks/{deck.Id}", deck);
    }

    [HttpPut("{deckId}")]
    public async Task<IActionResult> UpdateDeck(string deckId, [FromBody] UpdateDeckRequest request)
    {
        if (!ObjectId.TryParse(deckId, out _))
        {
            return BadRequest(new { message = "deckId no es valido." });
        }

        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        var decksCollection = GetDecksCollection();
        var existingDeck = await decksCollection
            .Find(deck => deck.Id == deckId && deck.OwnerUserId == userId)
            .FirstOrDefaultAsync();

        if (existingDeck is null)
        {
            return NotFound(new { message = "Baraja no encontrada." });
        }

        if (!DeckRequestValidator.TryValidateUpdate(request, existingDeck.GameType, out var updateErrorMessage))
        {
            return BadRequest(new { message = updateErrorMessage });
        }

        existingDeck.Name = request.Name.Trim();
        existingDeck.Description = request.Description?.Trim() ?? string.Empty;
        existingDeck.Cards = request.Cards
            .Select(card => new DeckCard
            {
                CardId = card.CardId.Trim(),
                Name = card.Name.Trim(),
                ImageUrl = card.ImageUrl.Trim(),
                Quantity = card.Quantity
            })
            .ToList();
        existingDeck.UpdatedAtUtc = DateTime.UtcNow;

        await decksCollection.ReplaceOneAsync(deck => deck.Id == deckId, existingDeck);

        return Ok(existingDeck);
    }

    [HttpDelete("{deckId}")]
    public async Task<IActionResult> DeleteDeck(string deckId)
    {
        if (!ObjectId.TryParse(deckId, out _))
        {
            return BadRequest(new { message = "deckId no es valido." });
        }

        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        var decksCollection = GetDecksCollection();
        var result = await decksCollection.DeleteOneAsync(deck => deck.Id == deckId && deck.OwnerUserId == userId);

        return result.DeletedCount == 0
            ? NotFound(new { message = "Baraja no encontrada." })
            : NoContent();
    }

    private IMongoCollection<Deck> GetDecksCollection()
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        return database.GetCollection<Deck>(options.Value.DecksCollectionName);
    }
}
