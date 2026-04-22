namespace CBD.Api.Models;

public static class DeckGameTypes
{
    public const string Pokemon = "pokemon";
    public const string Magic = "magic";
    public const string Yugioh = "yugioh";

    public static bool TryNormalize(string? value, out string normalizedValue)
    {
        var candidate = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (candidate is Pokemon or Magic or Yugioh)
        {
            normalizedValue = candidate;
            return true;
        }

        normalizedValue = string.Empty;
        return false;
    }
}