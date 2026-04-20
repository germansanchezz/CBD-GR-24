using CBD.Api.Models;
using CBD.Api.Options;
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

decks.MapGet("", async (string ownerUserId, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var filter = string.IsNullOrWhiteSpace(ownerUserId)
        ? Builders<Deck>.Filter.Empty
        : Builders<Deck>.Filter.Eq(deck => deck.OwnerUserId, ownerUserId.Trim());

    var decks = await decksCollection
        .Find(filter)
        .SortByDescending(deck => deck.UpdatedAtUtc)
        .ToListAsync();

    return Results.Ok(decks);
});

decks.MapGet("/{deckId}", async (string deckId, string? ownerUserId, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!ObjectId.TryParse(deckId, out _))
    {
        return Results.BadRequest(new { message = "deckId no es valido." });
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var filter = Builders<Deck>.Filter.Eq(deck => deck.Id, deckId);

    if (!string.IsNullOrWhiteSpace(ownerUserId))
    {
        filter &= Builders<Deck>.Filter.Eq(deck => deck.OwnerUserId, ownerUserId.Trim());
    }

    var deck = await decksCollection.Find(filter).FirstOrDefaultAsync();

    return deck is null
        ? Results.NotFound(new { message = "Baraja no encontrada." })
        : Results.Ok(deck);
});

decks.MapPost("", async (CreateDeckRequest request, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    var name = request.Name.Trim();
    var description = request.Description?.Trim() ?? string.Empty;
    var ownerUserId = request.OwnerUserId.Trim();

    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ownerUserId))
    {
        return Results.BadRequest(new { message = "Name y ownerUserId son obligatorios." });
    }

    var deck = new Deck
    {
        Name = name,
        Description = description,
        OwnerUserId = ownerUserId,
        Cards = [],
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    await decksCollection.InsertOneAsync(deck);

    return Results.Created($"/api/decks/{deck.Id}", deck);
});

decks.MapPut("/{deckId}", async (string deckId, UpdateDeckRequest request, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!ObjectId.TryParse(deckId, out _))
    {
        return Results.BadRequest(new { message = "deckId no es valido." });
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var existingDeck = await decksCollection.Find(deck => deck.Id == deckId).FirstOrDefaultAsync();
    if (existingDeck is null)
    {
        return Results.NotFound(new { message = "Baraja no encontrada." });
    }

    existingDeck.Name = request.Name.Trim();
    existingDeck.Description = request.Description?.Trim() ?? string.Empty;
    existingDeck.UpdatedAtUtc = DateTime.UtcNow;

    await decksCollection.ReplaceOneAsync(deck => deck.Id == deckId, existingDeck);

    return Results.Ok(existingDeck);
});

decks.MapDelete("/{deckId}", async (string deckId, IMongoClient mongoClient, IOptions<MongoDbOptions> options) =>
{
    if (!ObjectId.TryParse(deckId, out _))
    {
        return Results.BadRequest(new { message = "deckId no es valido." });
    }

    var database = mongoClient.GetDatabase(options.Value.DatabaseName);
    var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);

    var result = await decksCollection.DeleteOneAsync(deck => deck.Id == deckId);

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

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string? Id, string Email, string DisplayName);
public sealed record CreateDeckRequest(string Name, string? Description, string OwnerUserId);
public sealed record UpdateDeckRequest(string Name, string? Description);
