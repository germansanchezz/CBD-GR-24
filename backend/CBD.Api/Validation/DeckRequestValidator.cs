using CBD.Api.Contracts.Decks;

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