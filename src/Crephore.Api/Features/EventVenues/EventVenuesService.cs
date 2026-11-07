using Crephore.Api.Common.Authorization;
using Crephore.Api.Common.Constants;
using Crephore.Api.Common.Exceptions;
using Crephore.Api.Data;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventVenues.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Crephore.Api.Features.EventVenues;

public interface IEventVenuesService
{
    Task<List<EventVenueDto>> GetAsync(int eventId, int userId);
    Task<List<EventVenueDto>> UpdateAsync(int eventId, int userId, UpdateEventVenuesRequestDto request);
}

public class EventVenuesService(AppDbContext context) : IEventVenuesService
{
    private readonly AppDbContext _context = context;

    public const int MaxVenues = 10;
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 300;
    public const int UrlMaxLength = 500;
    public const int NotesMaxLength = 1000;

    // Hosts of the map links: each, or one of its subdomains (e.g. www.google.com, maps.app.goo.gl)
    private static readonly string[] GoogleMapsHosts = ["google.com", "goo.gl"];
    private static readonly string[] AppleMapsHosts = ["maps.apple.com", "maps.apple", "apple.co"];

    // The event's venues in order (empty until the host adds one). Any access can read them.
    public async Task<List<EventVenueDto>> GetAsync(int eventId, int userId)
    {
        await _context.RequireEventAccessAsync(eventId, userId);

        var venues = await _context.EventVenues
            .AsNoTracking()
            .Where(v => v.EventId == eventId)
            .ToListAsync();

        return EventVenuesMapper.ToEventVenueDtos(venues);
    }

    // Replaces the venues: ones with an id are updated, new ones added, and saved ones left out deleted; their order is
    // the request's. Program items of a deleted venue move to the new main venue (the first), or to none when no
    // venues are left.
    public async Task<List<EventVenueDto>> UpdateAsync(int eventId, int userId, UpdateEventVenuesRequestDto request)
    {
        await _context.RequireEventAccessAsync(eventId, userId, EventAccess.Owner);

        var savedVenues = await _context.EventVenues
            .Where(v => v.EventId == eventId)
            .ToListAsync();

        var values = Validate(request, savedVenues);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var keptIds = request.Venues.Where(v => v.Id is not null).Select(v => v.Id!.Value).ToHashSet();
        var deleted = savedVenues.Where(v => !keptIds.Contains(v.Id)).ToList();

        var saved = savedVenues.ToDictionary(v => v.Id);
        var venues = new List<EventVenue>();
        foreach (var (item, index) in request.Venues.Select((item, index) => (item, index)))
        {
            if (item.Id is not { } id || !saved.TryGetValue(id, out var venue))
            {
                venue = new EventVenue { EventId = eventId };
                _context.EventVenues.Add(venue);
            }

            var value = values[index];
            venue.Name = value.Name;
            venue.Address = value.Address;
            venue.GoogleMapsUrl = value.GoogleMapsUrl;
            venue.AppleMapsUrl = value.AppleMapsUrl;
            venue.Latitude = value.Latitude;
            venue.Longitude = value.Longitude;
            venue.PhotoUrl = value.PhotoUrl;
            venue.Notes = value.Notes;
            venue.SortOrder = index;
            venues.Add(venue);
        }

        // Saved first, so a new main venue has an id for the deleted venues' items to move to
        await _context.SaveChangesAsync();

        if (deleted.Count > 0)
        {
            var deletedIds = deleted.Select(v => v.Id).ToList();
            int? mainVenueId = venues.Count > 0 ? venues[0].Id : null;

            await _context.EventProgramItems
                .Where(i => i.VenueId != null && deletedIds.Contains(i.VenueId.Value))
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.VenueId, mainVenueId));

            // Deleted in the database, not through the change tracker: removing them there would also null the venue of
            // any program item this context tracks (EF applies SetNull to tracked items, with their old venue id),
            // undoing the move above
            await _context.EventVenues
                .Where(v => deletedIds.Contains(v.Id))
                .ExecuteDeleteAsync();
            deleted.ForEach(venue => _context.Entry(venue).State = EntityState.Detached);
        }

        await transaction.CommitAsync();

        return EventVenuesMapper.ToEventVenueDtos(venues);
    }

    // Helpers
    // A venue's checked and normalized values, ready to save
    private record ValidatedVenue(
        string Name,
        string? Address,
        string? GoogleMapsUrl,
        string? AppleMapsUrl,
        decimal? Latitude,
        decimal? Longitude,
        string? PhotoUrl,
        string? Notes);

    // Throws an AppValidationException with every error, keyed by the venue's position ("venues[1].name"). Returns
    // each venue's values: text trimmed, optional text left blank as null.
    private static List<ValidatedVenue> Validate(UpdateEventVenuesRequestDto request, List<EventVenue> savedVenues)
    {
        if (request.Venues.Count > MaxVenues)
            throw new AppValidationException(new Dictionary<string, List<string>>
            {
                ["venues"] = [$"An event can have at most {MaxVenues} venues."],
            });

        var errors = new Dictionary<string, List<string>>();
        var values = new List<ValidatedVenue>();

        var savedIds = savedVenues.Select(v => v.Id).ToHashSet();
        var requestedIds = new HashSet<int>();

        foreach (var (item, index) in request.Venues.Select((item, index) => (item, index)))
        {
            var key = $"venues[{index}]";

            if (item.Id is { } id && (!savedIds.Contains(id) || !requestedIds.Add(id)))
                AddError(errors, "venues", $"Venue {index + 1} is no longer one of the event's venues. Reload the page to see the latest version.");

            var name = Optional(item.Name);
            if (name is null)
                AddError(errors, $"{key}.name", "Name is required.");
            else
                CheckLength(name, NameMaxLength, $"{key}.name", "Name", errors);

            var address = Optional(item.Address);
            CheckLength(address, AddressMaxLength, $"{key}.address", "Address", errors);

            var googleMapsUrl = Optional(item.GoogleMapsUrl);
            if (googleMapsUrl is not null && !IsHttpsLinkOn(googleMapsUrl, GoogleMapsHosts))
                AddError(errors, $"{key}.googleMapsUrl", "Google Maps link must be a link from Google Maps.");
            else
                CheckLength(googleMapsUrl, UrlMaxLength, $"{key}.googleMapsUrl", "Google Maps link", errors);

            var appleMapsUrl = Optional(item.AppleMapsUrl);
            if (appleMapsUrl is not null && !IsHttpsLinkOn(appleMapsUrl, AppleMapsHosts))
                AddError(errors, $"{key}.appleMapsUrl", "Apple Maps link must be a link from Apple Maps.");
            else
                CheckLength(appleMapsUrl, UrlMaxLength, $"{key}.appleMapsUrl", "Apple Maps link", errors);

            // Stored with 6 decimals (about 10 cm), rounded like MySQL does, so the saved pin is returned as it will be read back
            var latitude = item.Latitude is { } lat ? Math.Round(lat, 6, MidpointRounding.AwayFromZero) : (decimal?)null;
            var longitude = item.Longitude is { } lng ? Math.Round(lng, 6, MidpointRounding.AwayFromZero) : (decimal?)null;
            if (latitude is < -90 or > 90)
                AddError(errors, $"{key}.latitude", "Latitude is not a valid latitude.");
            if (longitude is < -180 or > 180)
                AddError(errors, $"{key}.longitude", "Longitude is not a valid longitude.");
            if (latitude is null != longitude is null)
                AddError(errors, $"{key}.latitude", "Latitude and longitude go together.");

            var photoUrl = Optional(item.PhotoUrl);
            if (photoUrl is not null && !IsHttpLink(photoUrl))
                AddError(errors, $"{key}.photoUrl", "Photo must be a link.");
            else
                CheckLength(photoUrl, UrlMaxLength, $"{key}.photoUrl", "Photo", errors);

            var notes = Optional(item.Notes);
            CheckLength(notes, NotesMaxLength, $"{key}.notes", "Notes", errors);

            values.Add(new ValidatedVenue(name ?? string.Empty, address, googleMapsUrl, appleMapsUrl, latitude, longitude,
                photoUrl, notes));
        }

        if (errors.Count > 0)
            throw new AppValidationException(errors);

        return values;
    }

    // Optional text: left empty (or blank) is none
    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // An absolute https link whose host is one of `hosts` or a subdomain of one
    private static bool IsHttpsLinkOn(string value, string[] hosts) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        hosts.Any(host => uri.Host == host || uri.Host.EndsWith($".{host}", StringComparison.Ordinal));

    // An absolute http(s) link, e.g. an uploaded photo's URL
    private static bool IsHttpLink(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static void CheckLength(string? value, int maxLength, string key, string label, Dictionary<string, List<string>> errors)
    {
        if (value is not null && value.Length > maxLength)
            AddError(errors, key, $"{label} must be {maxLength} characters or fewer.");
    }

    private static void AddError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
            errors[key] = messages = [];
        messages.Add(message);
    }
}
