using System.Net;

namespace Crephore.Api.Common.Exceptions;

public class ForbiddenException(string message) : AppException(message, (int)HttpStatusCode.Forbidden)
{
}
