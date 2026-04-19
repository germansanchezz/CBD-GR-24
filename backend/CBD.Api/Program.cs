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
