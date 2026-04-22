using CBD.Api.Models;
using CBD.Api.Options;
using CBD.Api.Contracts.Decks;
using CBD.Api.Validation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
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

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string? Id, string Email, string DisplayName);
