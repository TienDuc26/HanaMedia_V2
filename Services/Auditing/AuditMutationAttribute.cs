using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HanaMedia.Services.Auditing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuditMutationAttribute : Attribute, IAsyncActionFilter
{
    public AuditMutationAttribute(string module, string actionType, string detail)
    {
        Module = module;
        ActionType = actionType;
        Detail = detail;
    }

    public string Module { get; }

    public string ActionType { get; }

    public string Detail { get; }

    public string? TargetType { get; set; }

    public string? TargetIdArgument { get; set; }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null || IsFailureResult(executed.Result))
        {
            return;
        }

        var targetId = ResolveTargetId(context.ActionArguments);
        var auditService = context.HttpContext.RequestServices
            .GetRequiredService<ISystemAuditService>();
        await auditService.WriteAsync(
            new AuditEvent(
                Module,
                ActionType,
                Detail,
                TargetType: TargetType,
                TargetId: targetId),
            context.HttpContext.RequestAborted);
    }

    private string? ResolveTargetId(IDictionary<string, object?> arguments)
    {
        if (string.IsNullOrWhiteSpace(TargetIdArgument) ||
            !arguments.TryGetValue(TargetIdArgument, out var value))
        {
            return null;
        }

        return value?.ToString();
    }

    private static bool IsFailureResult(IActionResult? result)
        => result is ObjectResult { StatusCode: >= 400 }
            or StatusCodeResult { StatusCode: >= 400 };
}
