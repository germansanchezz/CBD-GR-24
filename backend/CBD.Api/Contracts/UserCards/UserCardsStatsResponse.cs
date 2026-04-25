namespace CBD.Api.Contracts.UserCards;

public sealed record UserCardsStatsResponse(
    int TotalUniqueCards,
    int TotalOwnedCopies,
    int DistinctSets,
    decimal AverageCopiesPerCard,
    List<UserCardsByFieldStat> GameTypeDistribution,
    List<UserCardsByFieldStat> RarityDistribution,
    List<UserCardsByFieldStat> SetDistribution,
    List<UserCardsTopCardStat> TopCards);

public sealed record UserCardsByFieldStat(string Label, int TotalOwnedCopies);

public sealed record UserCardsTopCardStat(string ExternalCardId, string Name, string GameType, int TotalOwnedCopies);
