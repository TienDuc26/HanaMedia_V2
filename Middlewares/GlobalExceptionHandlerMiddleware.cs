using System.Net.Mime;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

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

        // Bóc tách DbUpdateException để lấy thông điệp gốc từ SQL Server
        // (Constraint, FK, unique index, …) thay vì trả message chung chung.
        var (statusCode, userMessage) = ResolveStatusAndMessage(exception);

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

    private (int StatusCode, string Message) ResolveStatusAndMessage(Exception exception)
    {
        // Lấy inner exception gốc (bỏ qua DbUpdateException / wrapper)
        var root = exception;
        while (root.InnerException != null)
            root = root.InnerException;

        // Trong Development, trả thông điệp chi tiết để dev thấy ngay nguyên nhân
        if (exception is DbUpdateException dbEx)
        {
            var detail = ExtractSqlDetail(dbEx);
            if (_env.IsDevelopment())
            {
                return (StatusCodes.Status400BadRequest,
                    string.IsNullOrEmpty(detail)
                        ? dbEx.Message
                        : $"Lỗi ràng buộc dữ liệu: {detail}");
            }
            return (StatusCodes.Status400BadRequest,
                "Dữ liệu không thỏa mãn ràng buộc (khóa ngoại, CHECK constraint, hoặc unique index). Vui lòng kiểm tra lại.");
        }

        var statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        var userMessage = _env.IsDevelopment()
            ? (string.IsNullOrEmpty(root.Message) ? exception.Message : root.Message)
            : "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại hoặc liên hệ quản trị viên.";

        return (statusCode, userMessage);
    }

    private static string ExtractSqlDetail(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner == null) return string.Empty;

        var msg = inner.Message;
        // Cắt phần "The INSERT statement conflicted with the CHECK constraint ..."
        // để lấy tên constraint + bảng cho dễ debug.
        var idx = msg.IndexOf("constraint", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return msg.Substring(idx).TrimEnd('.');

        return msg;
    }
}

public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
