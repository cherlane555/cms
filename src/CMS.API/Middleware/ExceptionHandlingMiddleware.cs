namespace CMS.API.Middleware;

/// <summary>
/// Global exception handler: logs the full exception (message + stack trace) server-side and
/// returns one consistent, safe 500 JSON body ({ "message": "..." }) — never the stack trace,
/// SQL text, or connection details.
/// Meaningful responses (401 / 403 / validation 400) are produced without throwing, so they
/// pass through this middleware untouched.
/// </summary>
public class ExceptionHandlingMiddleware
{
    /// <summary>The only error detail a client ever sees for an unexpected failure.</summary>
    public const string GenericMessage = "An unexpected error occurred.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception handling {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // Too late to replace the body — rethrow so the server aborts the response.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = GenericMessage });
        }
    }
}
