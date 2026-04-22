using CBD.Api.Contracts.Decks;
using CBD.Api.Models;
using System.Globalization;

namespace CBD.Api.Validation;

public static class DeckRequestValidator
{
    public const int MaxCopiesPerCardName = 4;
    public const int MaxCardsPerDeck = 60;

    public static bool TryValidateCreate(CreateDeckRequest request, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errorMessage = "Name es obligatorio.";
            return false;
        }

        if (!DeckGameTypes.TryNormalize(request.GameType, out _))
        {
            errorMessage = "GameType debe ser pokemon, magic o yugioh.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static bool TryValidateUpdate(UpdateDeckRequest request, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errorMessage = "Name es obligatorio.";
            return false;
        }

        if (request.Cards is null)
        {
            errorMessage = "Cards es obligatorio.";
            return false;
        }

        var totalCards = 0;
        var copiesByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in request.Cards)
        {
            if (string.IsNullOrWhiteSpace(card.CardId) ||
                string.IsNullOrWhiteSpace(card.Name) ||
                string.IsNullOrWhiteSpace(card.ImageUrl))
            {
                errorMessage = "Cada carta debe incluir cardId, name e imageUrl.";
                return false;
            }

            if (card.Quantity <= 0)
            {
                errorMessage = "La cantidad de cada carta debe ser mayor que 0.";
                return false;
            }

            totalCards += card.Quantity;
            if (totalCards > MaxCardsPerDeck)
            {
                errorMessage = $"Una baraja no puede superar {MaxCardsPerDeck} cartas.";
                return false;
            }

            var normalizedName = card.Name.Trim().ToUpper(CultureInfo.InvariantCulture);
            copiesByName[normalizedName] = copiesByName.GetValueOrDefault(normalizedName) + card.Quantity;

            if (copiesByName[normalizedName] > MaxCopiesPerCardName)
            {
                errorMessage = $"No puedes tener mas de {MaxCopiesPerCardName} copias con el mismo nombre.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}