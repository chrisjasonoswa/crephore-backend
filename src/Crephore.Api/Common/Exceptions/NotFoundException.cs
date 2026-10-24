using System.Net;

namespace Crephore.Api.Common.Exceptions;

public class NotFoundException(string message) : AppException(message, (int)HttpStatusCode.NotFound)
{
}
