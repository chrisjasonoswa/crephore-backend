using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Features.Auth.Dtos;
using Crephore.Api.Features.AppUsers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.Auth;

[Route(ApiRoutes.Auth.Route)]
[ApiController]
public class AuthController(
    IAuthService authService,
    ICurrentUser currentUser
) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly ICurrentUser _currentUser = currentUser;


    [HttpPost(ApiRoutes.Auth.Signup.RelativeRoute)]
    public async Task<IActionResult> Signup([FromBody] SignupRequestDto request)
    {
        AppUserDto user = await _authService.SignupAsync(request);
        return Ok(ApiResponse<AppUserDto>.Ok(user));
    }


    [HttpPost(ApiRoutes.Auth.Login.RelativeRoute)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        LoginResponseDto result = await _authService.LoginAsync(request.Email, request.Password);

        // Set auth cookies for web clients
        SetAuthCookies(result);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }


    [HttpPost(ApiRoutes.Auth.Refresh.RelativeRoute)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto? request)
    {
        // Get refresh token either from cookies (web) or from the request body (API clients)
        var refreshToken =
            Request.Cookies[AuthCookies.RefreshToken]
            ?? request?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException(
                "Your session has expired.",
                new Dictionary<string, List<string>>
                {
                    ["refreshToken"] = ["missing"]
                });

        RefreshResponseDto result = await _authService.RefreshAsync(refreshToken);
        SetAuthCookies(result);
        return Ok(ApiResponse<RefreshResponseDto>.Ok(result));
    }


    [HttpPost(ApiRoutes.Auth.Logout.RelativeRoute)]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        int userId = _currentUser.GetUserId();
        string sessionId = _currentUser.GetSessionId();

        await _authService.LogoutAsync(userId, sessionId);

        ClearAuthCookies();

        return Ok(ApiResponse<object?>.Ok(null));
    }


    [HttpGet(ApiRoutes.Auth.Me.RelativeRoute)]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        int userId = _currentUser.GetUserId();
        AppUserDto user = await _authService.GetCurrentUserAsync(userId);
        return Ok(ApiResponse<AppUserDto>.Ok(user));
    }


    // Helpers

    // The refresh token cookie is only sent to the refresh endpoint, not with every API request
    private const string RefreshCookiePath = "/" + ApiRoutes.Auth.Refresh.FullPath;

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AuthCookies.AccessToken);
        Response.Cookies.Delete(AuthCookies.RefreshToken, new CookieOptions { Path = RefreshCookiePath });
        Response.Cookies.Delete(AuthCookies.SessionId);
    }

    private void SetAuthCookies(ITokenSessionDto result)
    {
        Response.Cookies.Append(AuthCookies.AccessToken, result.AccessToken, CookieOptions(result.AccessTokenExpiresAt));
        Response.Cookies.Append(
            AuthCookies.RefreshToken,
            result.RefreshToken,
            CookieOptions(result.RefreshTokenExpiresAt, RefreshCookiePath));
        Response.Cookies.Append(AuthCookies.SessionId, result.SessionId, CookieOptions(result.RefreshTokenExpiresAt));
    }

    private static CookieOptions CookieOptions(DateTimeOffset expires, string path = "/") => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Expires = expires,
        Path = path,
    };
}
