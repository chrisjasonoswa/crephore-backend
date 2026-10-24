using System.Security.Claims;
using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Exceptions;

namespace Crephore.Api.Common.Authentication;

public interface ICurrentUser
{
    bool IsAuthenticated();

    int GetUserId();
    string GetSessionId();

    bool IsSuperAdmin();
}

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal User =>
        _httpContextAccessor.HttpContext?.User
        ?? throw new UnauthorizedException("No current user context.");

    public bool IsAuthenticated() =>
        User.Identity?.IsAuthenticated ?? false;

    public int GetUserId() =>
        int.TryParse(
            FirstNonEmpty(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue("nameid"),
                User.FindFirstValue("sub")),
            out var userId) ? userId : throw new UnauthorizedException("Missing required claim: user id.");

    public string GetSessionId() =>
        User.FindFirstValue(CustomClaimTypes.SessionId)
        ?? throw new UnauthorizedException("Missing required claim: sid.");

    public bool IsSuperAdmin() =>
        User.IsInRole(DefaultRoles.SuperAdmin);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
