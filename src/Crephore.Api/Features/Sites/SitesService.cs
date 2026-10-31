using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.Sites.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.Sites;

public interface ISitesService
{
    Task<SiteDto> GetAsync(string subdomain);
}

public class SitesService(AppDbContext context) : ISitesService
{
    private readonly AppDbContext _context = context;

    // Anyone can read a published website; unpublished ones and unknown addresses are the same 404,
    // so an unpublished website's existence is not revealed.
    public async Task<SiteDto> GetAsync(string subdomain)
    {
        var key = subdomain.Trim().ToLowerInvariant();

        var site = await _context.EventWebsites
            .AsNoTracking()
            .Where(w => w.Event.Subdomain == key && w.PublishedContent != null)
            .Select(w => new
            {
                w.PublishedContent,
                PublishedAt = w.PublishedAt!.Value,
                w.Event.Subdomain,
                w.Event.Name,
                w.Event.StartsAt,
                w.Event.Timezone,
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("Website not found.");

        var published = EventWebsitesMapper.ReadPublishedContent(site.PublishedContent)!;
        return new SiteDto
        {
            Subdomain = site.Subdomain,
            EventName = site.Name,
            StartsAt = site.StartsAt,
            Timezone = site.Timezone,
            PublishedAt = site.PublishedAt,
            Theme = published.Theme,
            Sections = published.Sections,
        };
    }
}
