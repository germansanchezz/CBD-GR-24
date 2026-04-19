using CBD.Api.Options;
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
        policy.WithOrigins("http://localhost:5173")
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

app.Run();
