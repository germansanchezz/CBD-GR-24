using CBD.Api.Contracts.Decks;
using MongoDB.Bson;

namespace CBD.Api.Validation;

public static class DeckRequestValidator
{
    public static bool TryValidateCreate(CreateDeckRequest request, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errorMessage = "Name es obligatorio.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.OwnerUserId))
        {
            errorMessage = "OwnerUserId es obligatorio.";
            return false;
        }

        if (!ObjectId.TryParse(request.OwnerUserId.Trim(), out _))
        {
            errorMessage = "OwnerUserId no es valido.";
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

        errorMessage = string.Empty;
        return true;
    }
}