using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crephore.Api.Features.Media;

/*
 * Photos in a website section's content are references to the event's media, { "mediaId": 12 }, never URLs: a URL
 * depends on where files are served from (the API in development, Backblaze B2 when hosted), so a stored one would
 * break whenever that changes. Any object with a "mediaId", at any depth, is a reference (the API knows no other
 * content fields). Saving keeps only the id; reading adds the URL, { "mediaId": 12, "url": "…" }, for the web app.
 */
public static class MediaReferences
{
    public const string IdProperty = "mediaId";
    public const string UrlProperty = "url";

    // The content as stored: every reference reduced to its id (the URL the web app sends back is dropped), with the
    // ids it uses. False when a reference's id isn't a positive whole number.
    public static bool TryToStored(JsonElement content, out JsonNode stored, out HashSet<int> ids)
    {
        var found = new HashSet<int>();
        var valid = true;
        stored = JsonNode.Parse(content.GetRawText())!;
        Rewrite(stored, reference =>
        {
            if (!TryGetId(reference, out var id))
            {
                valid = false;
                return reference.DeepClone();
            }
            found.Add(id);
            return new JsonObject { [IdProperty] = id };
        });
        ids = found;
        return valid;
    }

    // The ids of every reference in stored content
    public static IEnumerable<int> IdsIn(string content) => IdsIn(JsonNode.Parse(content));

    public static IEnumerable<int> IdsIn(JsonElement content) => IdsIn(JsonNode.Parse(content.GetRawText()));

    // The content with each reference's URL (`urls` by media id). A reference to media that no longer exists becomes
    // null, which the web app reads as no photo.
    public static JsonElement WithUrls(string content, IReadOnlyDictionary<int, string> urls) =>
        WithUrls(JsonNode.Parse(content), urls);

    public static JsonElement WithUrls(JsonElement content, IReadOnlyDictionary<int, string> urls) =>
        WithUrls(JsonNode.Parse(content.GetRawText()), urls);

    // Helpers
    private static JsonElement WithUrls(JsonNode? content, IReadOnlyDictionary<int, string> urls)
    {
        Rewrite(content, reference =>
            TryGetId(reference, out var id) && urls.TryGetValue(id, out var url)
                ? new JsonObject { [IdProperty] = id, [UrlProperty] = url }
                : null);
        return JsonSerializer.SerializeToElement(content);
    }

    private static List<int> IdsIn(JsonNode? content)
    {
        var ids = new List<int>();
        Rewrite(content, reference =>
        {
            if (TryGetId(reference, out var id))
                ids.Add(id);
            return reference.DeepClone();
        });
        return ids;
    }

    private static bool TryGetId(JsonObject reference, out int id)
    {
        id = 0;
        return reference[IdProperty] is JsonValue value
            && value.GetValueKind() == JsonValueKind.Number
            && value.TryGetValue(out id)
            && id > 0;
    }

    // Replaces every reference below `node` with what `replace` returns (a new node, not attached anywhere)
    private static void Rewrite(JsonNode? node, Func<JsonObject, JsonNode?> replace)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (IsReference(value))
                        obj[key] = replace((JsonObject)value!);
                    else
                        Rewrite(value, replace);
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (IsReference(array[i]))
                        array[i] = replace((JsonObject)array[i]!);
                    else
                        Rewrite(array[i], replace);
                }
                break;
        }
    }

    private static bool IsReference(JsonNode? node) => node is JsonObject obj && obj.ContainsKey(IdProperty);
}
