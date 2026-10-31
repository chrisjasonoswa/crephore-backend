using Crephore.Api.Common.Constants;

namespace Crephore.Api.Features.Media.Dtos;

public class MediaDto
{
    public int Id { get; set; }
    public MediaType Type { get; set; }
    public MediaPurpose Purpose { get; set; }
    // Where browsers load the file from
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
