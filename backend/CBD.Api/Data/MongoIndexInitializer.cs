using CBD.Api.Models;
using CBD.Api.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CBD.Api.Data;

public static class MongoIndexInitializer
{
    public static async Task EnsureIndexesAsync(IMongoClient mongoClient, IOptions<MongoDbOptions> options, CancellationToken cancellationToken = default)
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        var userCards = database.GetCollection<UserCard>(options.Value.UserCardsCollectionName);

        var uniqueExternalCardIndex = new CreateIndexModel<UserCard>(
            Builders<UserCard>.IndexKeys
                .Ascending(card => card.UserId)
                .Ascending(card => card.GameType)
                .Ascending(card => card.ExternalCardId),
            new CreateIndexOptions { Unique = true, Name = "ux_user_game_external_card" });

        var updatedAtIndex = new CreateIndexModel<UserCard>(
            Builders<UserCard>.IndexKeys
                .Ascending(card => card.UserId)
                .Ascending(card => card.GameType)
                .Descending(card => card.UpdatedAtUtc),
            new CreateIndexOptions { Name = "ix_user_game_updated_at" });

        var rarityIndex = new CreateIndexModel<UserCard>(
            Builders<UserCard>.IndexKeys
                .Ascending(card => card.UserId)
                .Ascending(card => card.GameType)
                .Ascending(card => card.Rarity),
            new CreateIndexOptions { Name = "ix_user_game_rarity" });

        var setIndex = new CreateIndexModel<UserCard>(
            Builders<UserCard>.IndexKeys
                .Ascending(card => card.UserId)
                .Ascending(card => card.GameType)
                .Ascending(card => card.SetName),
            new CreateIndexOptions { Name = "ix_user_game_set" });

        var nameIndex = new CreateIndexModel<UserCard>(
            Builders<UserCard>.IndexKeys
                .Ascending(card => card.UserId)
                .Ascending(card => card.GameType)
                .Ascending(card => card.Name),
            new CreateIndexOptions { Name = "ix_user_game_name" });

        await userCards.Indexes.CreateManyAsync(
            [uniqueExternalCardIndex, updatedAtIndex, rarityIndex, setIndex, nameIndex],
            cancellationToken);
    }
}
