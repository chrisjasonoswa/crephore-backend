namespace Crephore.Api.Common.Storage;

/*
 * Where uploaded files live. Keys are paths chosen by the API (e.g. "events/12/3f2a….jpg"), never the
 * uploader's file name. Local development writes to a folder the API serves (LocalFileStorage); hosted
 * environments will use Backblaze B2 through its S3-compatible API (AWS SDK) behind the same interface.
 */
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    // The public URL the file is served at, e.g. "http://localhost:5180/uploads/events/12/3f2a….jpg"
    string GetUrl(string key);
}
