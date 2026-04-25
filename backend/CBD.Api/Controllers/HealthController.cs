using CBD.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CBD.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IMongoClient mongoClient, IOptions<MongoDbOptions> options) : ControllerBase
{
    [HttpGet("mongo")]
    public async Task<IActionResult> GetMongoHealth()
    {
        try
        {
            var database = mongoClient.GetDatabase(options.Value.DatabaseName);
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return Ok(new { status = "ok", database = options.Value.DatabaseName });
        }
        catch (Exception exception)
        {
            return Problem(title: "MongoDB connection failed", detail: exception.Message, statusCode: 503);
        }
    }
}
