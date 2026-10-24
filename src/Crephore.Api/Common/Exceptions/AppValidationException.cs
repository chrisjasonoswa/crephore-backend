using System.Net;

namespace Crephore.Api.Common.Exceptions;

public class AppValidationException : AppException
{
    public AppValidationException(string message, Dictionary<string, List<string>>? errors = null)
        : base(message, (int)HttpStatusCode.BadRequest, errors)
    {
    }

    public AppValidationException(Dictionary<string, List<string>>? errors = null)
        : base("Some fields are invalid.", (int)HttpStatusCode.BadRequest, errors)
    {
    }
}
