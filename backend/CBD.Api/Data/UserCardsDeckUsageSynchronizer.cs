using CBD.Api.Models;
using CBD.Api.Options;
using MongoDB.Driver;

namespace CBD.Api.Data;

public static class UserCardsDeckUsageSynchronizer
{
    public static async Task SynchronizeForUserAsync(
        IMongoClient mongoClient,
        MongoDbOptions options,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var database = mongoClient.GetDatabase(options.DatabaseName);
        var decksCollection = database.GetCollection<Deck>(options.DecksCollectionName);
        var userCardsCollection = database.GetCollection<UserCard>(options.UserCardsCollectionName);

        var decks = await decksCollection
            .Find(deck => deck.OwnerUserId == userId)
            .ToListAsync(cancellationToken);

        var usageByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var deck in decks)
        {
            var gameType = deck.GameType.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(gameType))
            {
                continue;
            }

            foreach (var card in deck.Cards)
            {
                var externalCardId = card.CardId.Trim();
                if (string.IsNullOrWhiteSpace(externalCardId) || card.Quantity <= 0)
                {
                    continue;
                }

                var key = BuildKey(gameType, externalCardId);
                usageByKey[key] = usageByKey.GetValueOrDefault(key) + card.Quantity;
            }
        }

        var userCards = await userCardsCollection
            .Find(card => card.UserId == userId)
            .ToListAsync(cancellationToken);

        if (userCards.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var writes = new List<WriteModel<UserCard>>();

        foreach (var userCard in userCards)
        {
            if (string.IsNullOrWhiteSpace(userCard.Id))
            {
                continue;
            }

            var key = BuildKey(userCard.GameType, userCard.ExternalCardId);
            usageByKey.TryGetValue(key, out var quantityInDecks);

            if (userCard.QuantityInDecks == quantityInDecks)
            {
                continue;
            }

            var filter = Builders<UserCard>.Filter.Eq(card => card.Id, userCard.Id);
            var update = Builders<UserCard>.Update
                .Set(card => card.QuantityInDecks, quantityInDecks)
                .Set(card => card.UpdatedAtUtc, now);

            writes.Add(new UpdateOneModel<UserCard>(filter, update));
        }

        if (writes.Count > 0)
        {
            await userCardsCollection.BulkWriteAsync(writes, cancellationToken: cancellationToken);
        }
    }

    private static string BuildKey(string gameType, string externalCardId)
    {
        return $"{gameType.Trim().ToLowerInvariant()}::{externalCardId.Trim()}";
    }
}
