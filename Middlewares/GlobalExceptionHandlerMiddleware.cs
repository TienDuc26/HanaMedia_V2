using System.Net.Mime;
using System.Text.Json;

namespace HanaMedia.Middlewares;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _log;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        IHostEnvironment env,
        ILogger<GlobalExceptionHandlerMiddleware> log)
    {
        _next = next;
        _env = env;
        _log = log;
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

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log exception phía server
        _log.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        // Chỉ trả chi tiết exception ở môi trường Development
        // Production: che chi tiết, chỉ trả message chung
        string userMessage = _env.IsDevelopment()
            ? exception.Message
            : "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại hoặc liên hệ quản trị viên.";

        int statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var response = new
        {
            success = false,
            message = userMessage,
            code = statusCode == 500 ? "INTERNAL_ERROR" : exception.GetType().Name.ToUpperInvariant()
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsJsonAsync(response, options);
    }
}

public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
