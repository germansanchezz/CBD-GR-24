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

        var usageByKey = new Dictionary<string, DeckCardUsageSnapshot>(StringComparer.Ordinal);

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

                if (usageByKey.TryGetValue(key, out var existingUsage))
                {
                    existingUsage.QuantityInDecks += card.Quantity;
                }
                else
                {
                    usageByKey[key] = new DeckCardUsageSnapshot
                    {
                        GameType = gameType,
                        ExternalCardId = externalCardId,
                        Name = card.Name.Trim(),
                        ImageUrl = card.ImageUrl.Trim(),
                        QuantityInDecks = card.Quantity
                    };
                }
            }
        }

        var userCards = await userCardsCollection
            .Find(card => card.UserId == userId)
            .ToListAsync(cancellationToken);

        var userCardsByKey = userCards
            .Where(card => !string.IsNullOrWhiteSpace(card.ExternalCardId) && !string.IsNullOrWhiteSpace(card.GameType))
            .ToDictionary(card => BuildKey(card.GameType, card.ExternalCardId), card => card, StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var writes = new List<WriteModel<UserCard>>();

        foreach (var usage in usageByKey.Values)
        {
            var key = BuildKey(usage.GameType, usage.ExternalCardId);

            if (userCardsByKey.TryGetValue(key, out var existingCard))
            {
                if (string.IsNullOrWhiteSpace(existingCard.Id))
                {
                    continue;
                }

                var nextQuantityInDecks = usage.QuantityInDecks;
                var nextQuantityOwned = Math.Max(existingCard.QuantityOwned, nextQuantityInDecks);
                var nextName = string.IsNullOrWhiteSpace(existingCard.Name) ? usage.Name : existingCard.Name;
                var nextImageUrl = string.IsNullOrWhiteSpace(existingCard.ImageUrl) ? usage.ImageUrl : existingCard.ImageUrl;

                var requiresUpdate = existingCard.QuantityInDecks != nextQuantityInDecks
                    || existingCard.QuantityOwned != nextQuantityOwned
                    || !string.Equals(existingCard.Name, nextName, StringComparison.Ordinal)
                    || !string.Equals(existingCard.ImageUrl, nextImageUrl, StringComparison.Ordinal);

                if (!requiresUpdate)
                {
                    continue;
                }

                var filter = Builders<UserCard>.Filter.Eq(card => card.Id, existingCard.Id);
                var update = Builders<UserCard>.Update
                    .Set(card => card.QuantityInDecks, nextQuantityInDecks)
                    .Set(card => card.QuantityOwned, nextQuantityOwned)
                    .Set(card => card.Name, nextName)
                    .Set(card => card.ImageUrl, nextImageUrl)
                    .Set(card => card.UpdatedAtUtc, now);

                writes.Add(new UpdateOneModel<UserCard>(filter, update));
                continue;
            }

            var newUserCard = new UserCard
            {
                UserId = userId,
                GameType = usage.GameType,
                ExternalCardId = usage.ExternalCardId,
                Name = usage.Name,
                ImageUrl = usage.ImageUrl,
                SetName = string.Empty,
                Rarity = string.Empty,
                TypeLine = string.Empty,
                SearchTags = [],
                MainText = string.Empty,
                QuantityOwned = usage.QuantityInDecks,
                QuantityInDecks = usage.QuantityInDecks,
                AddedAtUtc = now,
                UpdatedAtUtc = now
            };

            writes.Add(new InsertOneModel<UserCard>(newUserCard));
        }

        foreach (var userCard in userCards)
        {
            if (string.IsNullOrWhiteSpace(userCard.Id))
            {
                continue;
            }

            var key = BuildKey(userCard.GameType, userCard.ExternalCardId);
            if (usageByKey.ContainsKey(key))
            {
                continue;
            }

            const int quantityInDecks = 0;

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

    private sealed class DeckCardUsageSnapshot
    {
        public string GameType { get; set; } = string.Empty;

        public string ExternalCardId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public int QuantityInDecks { get; set; }
    }

    private static string BuildKey(string gameType, string externalCardId)
    {
        return $"{gameType.Trim().ToLowerInvariant()}::{externalCardId.Trim()}";
    }
}
