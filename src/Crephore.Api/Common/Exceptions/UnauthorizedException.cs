using System.Net;

namespace Crephore.Api.Common.Exceptions;

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message)
        : base(message, (int)HttpStatusCode.Unauthorized)
    {
    }

    public UnauthorizedException(string message, Dictionary<string, List<string>>? errors)
        : base(message, (int)HttpStatusCode.Unauthorized, errors)
    {
    }

    public UnauthorizedException()
        : base("Unauthorized.", (int)HttpStatusCode.Unauthorized)
    {
    }
}
