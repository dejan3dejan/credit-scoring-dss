namespace CreditScoring.Api.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowedBeforePasswordChangeAttribute : Attribute;

public static class PasswordChangeGate
{
    public const string Code = "password_change_required";
    private const string PendingKey = "must_change_password";

    public static void Record(HttpContext context, bool mustChangePassword) => context.Items[PendingKey] = mustChangePassword;

    public static Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var pending = context.Items.TryGetValue(PendingKey, out var flag) && flag is true;
        if (!pending
            || !context.Request.Path.StartsWithSegments("/api")
            || context.GetEndpoint()?.Metadata.GetMetadata<AllowedBeforePasswordChangeAttribute>() is not null)
            return next(context);

        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Change your password first.",
            detail: "This account has a temporary password. Change it before using the application.",
            extensions: new Dictionary<string, object?> { ["code"] = Code }).ExecuteAsync(context);
    }
}
