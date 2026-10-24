using System.Security.Claims;

namespace Crephore.Api.Extensions;


public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(value))
            throw new UnauthorizedAccessException("User ID claim is missing.");

        if (!int.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("Invalid user ID claim.");

        return userId;
    }
}
