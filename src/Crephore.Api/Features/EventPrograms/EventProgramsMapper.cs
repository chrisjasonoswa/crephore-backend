using System.Globalization;
using Crephore.Api.Data.Entities;
using Crephore.Api.Features.EventPrograms.Dtos;

namespace Crephore.Api.Features.EventPrograms;

public static class EventProgramsMapper
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string TimeFormat = "HH:mm";

    // The items in their saved order
    public static EventProgramDto ToEventProgramDto(IEnumerable<EventProgramItem> items) => new()
    {
        Items = items.OrderBy(i => i.SortOrder).Select(ToEventProgramItemDto).ToList(),
    };

    public static EventProgramItemDto ToEventProgramItemDto(this EventProgramItem item) => new()
    {
        Id = item.Id,
        Date = item.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
        StartTime = item.StartTime?.ToString(TimeFormat, CultureInfo.InvariantCulture),
        EndTime = item.EndTime?.ToString(TimeFormat, CultureInfo.InvariantCulture),
        Title = item.Title,
        Description = item.Description,
        VenueId = item.VenueId,
        Speakers = item.Speakers,
    };
}
