using System.Net;
using System.Text.Json;

namespace RagWorkspace.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception has occurred");

        var code = HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred";
        
        // Customize response based on exception type
        switch (exception)
        {
            case KeyNotFoundException:
                code = HttpStatusCode.NotFound;
                message = "The requested resource was not found";
                break;
            case UnauthorizedAccessException:
                code = HttpStatusCode.Unauthorized;
                message = "Unauthorized access";
                break;
            case ArgumentException:
                code = HttpStatusCode.BadRequest;
                message = exception.Message;
                break;
            case InvalidOperationException:
                code = HttpStatusCode.BadRequest;
                message = exception.Message;
                break;
        }

        var result = JsonSerializer.Serialize(new { error = message });
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        
        return context.Response.WriteAsync(result);
    }
}