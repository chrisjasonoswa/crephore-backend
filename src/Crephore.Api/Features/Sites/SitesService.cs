using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Features.EventPrograms;
using Crephore.Api.Features.EventVenues;
using Crephore.Api.Features.EventWebsites;
using Crephore.Api.Features.Media;
using Crephore.Api.Features.Sites.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.Sites;

public interface ISitesService
{
    Task<SiteDto> GetAsync(string subdomain);
}

public class SitesService(AppDbContext context, IMediaService mediaService) : ISitesService
{
    private readonly AppDbContext _context = context;
    private readonly IMediaService _mediaService = mediaService;

    // Anyone can read a published website; unpublished ones and unknown addresses are the same 404,
    // so an unpublished website's existence is not revealed.
    public async Task<SiteDto> GetAsync(string subdomain)
    {
        var key = subdomain.Trim().ToLowerInvariant();

        var site = await _context.EventWebsites
            .AsNoTracking()
            // The program and the venues are two collections: one query each instead of their cross product
            .AsSplitQuery()
            .Where(w => w.Event.Subdomain == key && w.PublishedContent != null)
            .OrderBy(w => w.Id)
            .Select(w => new
            {
                w.EventId,
                w.PublishedContent,
                PublishedAt = w.PublishedAt!.Value,
                w.Event.Subdomain,
                w.Event.Name,
                w.Event.Type,
                w.Event.BornOn,
                w.Event.DiedOn,
                w.Event.StartsAt,
                w.Event.EndsAt,
                w.Event.Timezone,
                w.Event.ProgramItems,
                w.Event.Venues,
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("Website not found.");

        var published = EventWebsitesMapper.ReadPublishedContent(site.PublishedContent)!;

        // The photos of the sections and the venues are stored as media ids: one lookup for their URLs
        var mediaUrls = await _mediaService.GetUrlsAsync(site.EventId, published.Sections
            .SelectMany(s => MediaReferences.IdsIn(s.Content))
            .Concat(EventVenuesMapper.PhotoMediaIds(site.Venues)));
        foreach (var section in published.Sections)
            section.Content = MediaReferences.WithUrls(section.Content, mediaUrls);

        return new SiteDto
        {
            Subdomain = site.Subdomain,
            EventName = site.Name,
            VenueName = EventVenuesMapper.MainVenueName(site.Venues),
            EventType = site.Type,
            BornOn = site.BornOn,
            DiedOn = site.DiedOn,
            StartsAt = site.StartsAt,
            EndsAt = site.EndsAt,
            Timezone = site.Timezone,
            Program = EventProgramsMapper.ToEventProgramDto(site.ProgramItems),
            Venues = EventVenuesMapper.ToEventVenueDtos(site.Venues, mediaUrls),
            PublishedAt = site.PublishedAt,
            Theme = published.Theme,
            Sections = published.Sections,
        };
    }
}
