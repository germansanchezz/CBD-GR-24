using CBD.Api.Models;
using CBD.Api.Options;
using CBD.Api.Contracts.Decks;
using CBD.Api.Validation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient("external-cards", client =>
{
    client.DefaultRequestHeaders.UserAgent.Clear();
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CBD-GR-24", "1.0"));
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.Configure<MongoDbOptions>(builder.Configuration.GetSection(MongoDbOptions.SectionName));
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("frontend");

app.MapGet("/", () => Results.Ok(new { service = "CBD.Api", status = "running", framework = ".NET 8" }));
app.MapGet("/api/health/mongo", async (IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    try
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        return Results.Ok(new { status = "ok", database = options.Value.DatabaseName });
    }
    catch (Exception exception)
    {
        return Results.Problem(title: "MongoDB connection failed", detail: exception.Message, statusCode: 503);
    }
});

var cards = app.MapGroup("/api/cards");

cards.MapGet("/search", async (string gameType, string name, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    if (!DeckGameTypes.TryNormalize(gameType, out var normalizedGameType))
    {
        return Results.BadRequest(new { message = "gameType debe ser pokemon, magic o yugioh." });
    }

    var query = name.Trim();
    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.BadRequest(new { message = "name es obligatorio." });
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

        return Results.Ok(cards);
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(title: "Card provider request failed", detail: exception.Message, statusCode: 503);
    }
});

var decks = app.MapGroup("/api/decks");

decks.MapGet("", async (HttpContext httpContext, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!TryGetUserId(httpContext, out var userId, out var authError))
    {
        return authError;
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var filter = Builders<Deck>.Filter.Eq(deck => deck.OwnerUserId, userId);

    var decks = await decksCollection
        .Find(filter)
        .SortByDescending(deck => deck.UpdatedAtUtc)
        .ToListAsync();

    return Results.Ok(decks);
});

decks.MapGet("/{deckId}", async (string deckId, HttpContext httpContext, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!ObjectId.TryParse(deckId, out _))
    {
        return Results.BadRequest(new { message = "deckId no es valido." });
    }

    if (!TryGetUserId(httpContext, out var userId, out var authError))
    {
        return authError;
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var filter = Builders<Deck>.Filter.Eq(deck => deck.Id, deckId)
        & Builders<Deck>.Filter.Eq(deck => deck.OwnerUserId, userId);

    var deck = await decksCollection.Find(filter).FirstOrDefaultAsync();

    return deck is null
        ? Results.NotFound(new { message = "Baraja no encontrada." })
        : Results.Ok(deck);
});

decks.MapPost("", async (CreateDeckRequest request, HttpContext httpContext, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!DeckRequestValidator.TryValidateCreate(request, out var createErrorMessage))
    {
        return Results.BadRequest(new { message = createErrorMessage });
    }

    if (!TryGetUserId(httpContext, out var userId, out var authError))
    {
        return authError;
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

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    await decksCollection.InsertOneAsync(deck);

    return Results.Created($"/api/decks/{deck.Id}", deck);
});

decks.MapPut("/{deckId}", async (string deckId, UpdateDeckRequest request, HttpContext httpContext, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!ObjectId.TryParse(deckId, out _))
    {
        return Results.BadRequest(new { message = "deckId no es valido." });
    }

    if (!TryGetUserId(httpContext, out var userId, out var authError))
    {
        return authError;
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var existingDeck = await decksCollection
        .Find(deck => deck.Id == deckId && deck.OwnerUserId == userId)
        .FirstOrDefaultAsync();

    if (existingDeck is null)
    {
        return Results.NotFound(new { message = "Baraja no encontrada." });
    }

    if (!DeckRequestValidator.TryValidateUpdate(request, existingDeck.GameType, out var updateErrorMessage))
    {
        return Results.BadRequest(new { message = updateErrorMessage });
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

    return Results.Ok(existingDeck);
});

decks.MapDelete("/{deckId}", async (string deckId, HttpContext httpContext, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!ObjectId.TryParse(deckId, out _))
    {
        return Results.BadRequest(new { message = "deckId no es valido." });
    }

    if (!TryGetUserId(httpContext, out var userId, out var authError))
    {
        return authError;
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var result = await decksCollection.DeleteOneAsync(deck => deck.Id == deckId && deck.OwnerUserId == userId);

    return result.DeletedCount == 0
        ? Results.NotFound(new { message = "Baraja no encontrada." })
        : Results.NoContent();
});

var auth = app.MapGroup("/api/auth");

auth.MapPost("/register", async (RegisterRequest request, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var password = request.Password.Trim();
    var displayName = request.DisplayName.Trim();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(displayName))
    {
        return Results.BadRequest(new { message = "Email, password y displayName son obligatorios." });
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var usersCollection = database.GetCollection<User>(options.Value.UsersCollectionName);

    var existingUser = await usersCollection.Find(user => user.Email == email).FirstOrDefaultAsync();
    if (existingUser is not null)
    {
        return Results.Conflict(new { message = "Ya existe un usuario con ese email." });
    }

    var user = new User
    {
        Email = email,
        DisplayName = displayName,
        PasswordHash = ComputeSha256(password),
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    await usersCollection.InsertOneAsync(user);

    return Results.Ok(new AuthResponse(user.Id, user.Email, user.DisplayName));
});

auth.MapPost("/login", async (LoginRequest request, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var password = request.Password.Trim();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.BadRequest(new { message = "Email y password son obligatorios." });
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var usersCollection = database.GetCollection<User>(options.Value.UsersCollectionName);

    var user = await usersCollection.Find(existingUser => existingUser.Email == email).FirstOrDefaultAsync();
    if (user is null || user.PasswordHash != ComputeSha256(password))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new AuthResponse(user.Id, user.Email, user.DisplayName));
});

app.Run();

static string ComputeSha256(string value)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes);
}

static bool TryGetUserId(HttpContext httpContext, out string userId, out IResult? error)
{
    if (!httpContext.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader))
    {
        userId = string.Empty;
        error = Results.Unauthorized();
        return false;
    }

    var candidate = userIdHeader.ToString().Trim();
    if (!ObjectId.TryParse(candidate, out _))
    {
        userId = string.Empty;
        error = Results.BadRequest(new { message = "X-User-Id no es valido." });
        return false;
    }

    userId = candidate;
    error = null;
    return true;
}

static async Task<List<CardSearchResultResponse>> SearchPokemonCardsAsync(HttpClient httpClient, string name, CancellationToken cancellationToken)
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

        var imageBase = card.Image;

        if (string.IsNullOrWhiteSpace(imageBase))
        {
            var details = await httpClient.GetFromJsonAsync<TcgDexCardDetail>($"https://api.tcgdex.net/v2/es/cards/{card.Id}", cancellationToken);
            imageBase = details?.Image;
        }

        if (string.IsNullOrWhiteSpace(imageBase))
        {
            continue;
        }

        results.Add(new CardSearchResultResponse(card.Id, card.Name, $"{imageBase}/high.webp"));
    }

    return results;
}

static async Task<List<CardSearchResultResponse>> SearchMagicCardsAsync(HttpClient httpClient, string name, CancellationToken cancellationToken)
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
            return new CardSearchResultResponse(card.Id, displayName, imageUrl);
        })
        .Where(card => card is not null)
        .Cast<CardSearchResultResponse>()
        .ToList();
}

static async Task<List<CardSearchResultResponse>> SearchYugiohCardsAsync(HttpClient httpClient, string name, CancellationToken cancellationToken)
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

            return new CardSearchResultResponse(card.Id.ToString(), card.Name, imageUrl);
        })
        .Where(card => card is not null)
        .Cast<CardSearchResultResponse>()
        .ToList();
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string? Id, string Email, string DisplayName);
public sealed record CardSearchResultResponse(string CardId, string Name, string ImageUrl);

public sealed class TcgDexCardSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

public sealed class TcgDexCardDetail
{
    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

public sealed class ScryfallSearchResponse
{
    [JsonPropertyName("data")]
    public List<ScryfallCard>? Data { get; set; }
}

public sealed class ScryfallCard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("printed_name")]
    public string? PrintedName { get; set; }

    [JsonPropertyName("image_uris")]
    public ScryfallImageUris? ImageUris { get; set; }

    [JsonPropertyName("card_faces")]
    public List<ScryfallCardFace>? CardFaces { get; set; }
}

public sealed class ScryfallCardFace
{
    [JsonPropertyName("image_uris")]
    public ScryfallImageUris? ImageUris { get; set; }
}

public sealed class ScryfallImageUris
{
    [JsonPropertyName("normal")]
    public string? Normal { get; set; }
}

public sealed class YugiohSearchResponse
{
    [JsonPropertyName("data")]
    public List<YugiohCard>? Data { get; set; }
}

public sealed class YugiohCard
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("card_images")]
    public List<YugiohCardImage>? CardImages { get; set; }
}

public sealed class YugiohCardImage
{
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}
