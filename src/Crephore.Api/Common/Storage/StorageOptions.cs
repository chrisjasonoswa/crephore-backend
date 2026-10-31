namespace Crephore.Api.Common.Storage;

// The "Storage" configuration section: which IFileStorage to use, and its settings.
public class StorageOptions
{
    public const string SectionName = "Storage";
    public const string LocalProvider = "Local";

    // Only "Local" for now; "S3" (Backblaze B2) comes with hosted environments
    public string Provider { get; set; } = LocalProvider;

    public LocalStorageOptions Local { get; set; } = new();
}

public class LocalStorageOptions
{
    // Folder the files are written to; relative paths are under the API's content root
    public string RootPath { get; set; } = "uploads";

    // Path the API serves the folder at
    public string RequestPath { get; set; } = "/uploads";

    // Absolute URL of RequestPath as browsers reach it, e.g. "http://localhost:5180/uploads"
    public string PublicBaseUrl { get; set; } = string.Empty;
}
