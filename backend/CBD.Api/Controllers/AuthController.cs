using CBD.Api.Contracts.Auth;
using CBD.Api.Helpers;
using CBD.Api.Models;
using CBD.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CBD.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMongoClient mongoClient, IOptions<MongoDbOptions> options) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var password = request.Password.Trim();
        var displayName = request.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(displayName))
        {
            return BadRequest(new { message = "Email, password y displayName son obligatorios." });
        }

        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        var usersCollection = database.GetCollection<User>(options.Value.UsersCollectionName);

        var existingUser = await usersCollection.Find(user => user.Email == email).FirstOrDefaultAsync();
        if (existingUser is not null)
        {
            return Conflict(new { message = "Ya existe un usuario con ese email." });
        }

        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = PasswordHasher.ComputeSha256(password),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await usersCollection.InsertOneAsync(user);

        return Ok(new AuthResponse(user.Id, user.Email, user.DisplayName));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var password = request.Password.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "Email y password son obligatorios." });
        }

        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        var usersCollection = database.GetCollection<User>(options.Value.UsersCollectionName);

        var user = await usersCollection.Find(existingUser => existingUser.Email == email).FirstOrDefaultAsync();
        if (user is null || user.PasswordHash != PasswordHasher.ComputeSha256(password))
        {
            return Unauthorized();
        }

        return Ok(new AuthResponse(user.Id, user.Email, user.DisplayName));
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMyAccount()
    {
        if (!UserHeaderHelper.TryGetUserId(Request.Headers, out var userId, out var authError))
        {
            return authError!;
        }

        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        var usersCollection = database.GetCollection<User>(options.Value.UsersCollectionName);
        var decksCollection = database.GetCollection<Deck>(options.Value.DecksCollectionName);
        var userCardsCollection = database.GetCollection<UserCard>(options.Value.UserCardsCollectionName);

        var userDeleteResult = await usersCollection.DeleteOneAsync(user => user.Id == userId);
        if (userDeleteResult.DeletedCount == 0)
        {
            return NotFound(new { message = "Usuario no encontrado." });
        }

        await decksCollection.DeleteManyAsync(deck => deck.OwnerUserId == userId);
        await userCardsCollection.DeleteManyAsync(card => card.UserId == userId);

        return NoContent();
    }
}
