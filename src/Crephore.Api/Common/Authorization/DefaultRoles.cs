namespace Crephore.Api.Common.Authorization;

// Role claims added to the access token.
// Use with [Authorize(Roles = DefaultRoles.SuperAdmin)].
public static class DefaultRoles
{
    public const string SuperAdmin = "SuperAdmin";
}
