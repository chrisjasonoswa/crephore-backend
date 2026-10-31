namespace Crephore.Api.Common.Constants;

// What an uploaded file is for. WebsiteAsset: a photo the host put on their event website (a section's photos).
// Stored and serialized as its name; never rename a value once media uses it.
public enum MediaPurpose
{
    WebsiteAsset,
}
