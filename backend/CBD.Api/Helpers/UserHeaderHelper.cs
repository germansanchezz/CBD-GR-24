using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;

namespace CBD.Api.Helpers;

public static class UserHeaderHelper
{
    public static bool TryGetUserId(IHeaderDictionary headers, out string userId, out IActionResult? error)
    {
        if (!headers.TryGetValue("X-User-Id", out StringValues userIdHeader))
        {
            userId = string.Empty;
            error = new UnauthorizedResult();
            return false;
        }

        var candidate = userIdHeader.ToString().Trim();
        if (!ObjectId.TryParse(candidate, out _))
        {
            userId = string.Empty;
            error = new BadRequestObjectResult(new { message = "X-User-Id no es valido." });
            return false;
        }

        userId = candidate;
        error = null;
        return true;
    }
}
