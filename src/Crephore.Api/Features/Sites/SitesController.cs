using Crephore.Api.Common.Api;
using Crephore.Api.Features.Sites.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crephore.Api.Features.Sites;

// Published event websites, for guests: no sign-in. The web app loads them by the subdomain guests open.
[Route(ApiRoutes.Sites.Route)]
[ApiController]
[AllowAnonymous]
public class SitesController(ISitesService sitesService) : ControllerBase
{
    private readonly ISitesService _sitesService = sitesService;

    [HttpGet("{subdomain}")]
    public async Task<IActionResult> Get(string subdomain)
    {
        var site = await _sitesService.GetAsync(subdomain);
        return Ok(ApiResponse<SiteDto>.Ok(site));
    }
}
