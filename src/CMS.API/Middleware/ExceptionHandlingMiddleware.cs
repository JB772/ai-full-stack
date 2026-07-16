namespace CMS.API.Middleware;

/// <summary>
/// Catches any unhandled exception bubbling out of the downstream pipeline (controllers, repositories,
/// Dapper, …), logs the full detail server-side, and returns a single, consistent JSON body with a
/// generic 500 message. The exception message, stack trace, SQL text and connection details never
/// cross the wire.
///
/// Deliberately does NOT touch responses that are already meaningful: 401 (unauthenticated),
/// 403 (forbidden) and validation/400 results are produced without throwing, so they flow through
/// this middleware untouched.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>The only message a client ever sees for an unexpected server error.</summary>
    public const string GenericMessage = "An unexpected error occurred.";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Full detail (message + stack trace) stays on the server log.
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // The response is already on the wire — we can no longer replace it, so let it fail.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = GenericMessage });
        }
    }
}
