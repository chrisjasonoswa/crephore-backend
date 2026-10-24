namespace Crephore.Api.Common.Exceptions;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public Dictionary<string, List<string>>? Errors { get; }

    protected AppException(string message, int statusCode, Dictionary<string, List<string>>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}
