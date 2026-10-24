using System.Net;

namespace Crephore.Api.Common.Exceptions;

public class UnexpectedException : AppException
{
    public UnexpectedException(string message, Dictionary<string, List<string>>? errors = null)
        : base(message, (int)HttpStatusCode.InternalServerError, errors)
    {
    }

    public UnexpectedException(Dictionary<string, List<string>>? errors = null)
        : base("An unexpected error occured", (int)HttpStatusCode.InternalServerError, errors)
    {
    }
}
