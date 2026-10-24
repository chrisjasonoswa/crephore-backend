using Crephore.Api.Common.Api;
using Crephore.Api.Common.Authentication;
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

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AuthCookies.AccessToken);
        Response.Cookies.Delete(AuthCookies.SessionId);
    }

    private void SetAuthCookies(LoginResponseDto result)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = result.AccessTokenExpiresAt
        };

        Response.Cookies.Append(AuthCookies.AccessToken, result.AccessToken, cookieOptions);
        Response.Cookies.Append(AuthCookies.SessionId, result.SessionId, cookieOptions);
    }
}
