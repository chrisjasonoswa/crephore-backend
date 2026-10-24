using System.Net;
using Crephore.Api.Common.Api;
using Crephore.Api.Common.Exceptions;

namespace Crephore.Api.Middleware;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning(ex, "Handled application exception.");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex.StatusCode;

            var response = ApiResponse<object>.Fail(ex.Errors, ex.Message);

            await context.Response.WriteAsJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception.");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            // Do not leak exception details to clients; they are in the logs.
            var response = ApiResponse<object>.Fail("An unexpected server error occurred.");

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
