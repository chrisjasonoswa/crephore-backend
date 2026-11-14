using System.ComponentModel.DataAnnotations;

namespace Crephore.Api.Features.Media.Dtos;

// A photo: one of the event's media, by id (MediaDto.Id). Requests send the id; responses add the URL browsers load it
// from, which follows the storage, so it is never saved.
public class MediaReferenceDto
{
    [Range(1, int.MaxValue)]
    public int MediaId { get; set; }

    // Responses only; ignored in requests
    public string Url { get; set; } = string.Empty;
}
