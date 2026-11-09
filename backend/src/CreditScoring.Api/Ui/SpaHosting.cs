using Microsoft.Extensions.FileProviders;

namespace CreditScoring.Api.Ui;

public static class SpaHosting
{
    public const string BasePath = "/app";
    public const string RoutePattern = "app/{**path:nonfile}";
    public const string ShellFile = "app/index.html";

    public static void Map(WebApplication app) =>
        app.MapFallback(RoutePattern, context => ServeShellAsync(context, app.Environment.WebRootFileProvider))
            .AllowAnonymous();

    public static async Task ServeShellAsync(HttpContext context, IFileProvider webRoot)
    {
        var shell = webRoot.GetFileInfo(ShellFile);
        if (!shell.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                "The React app is not built. Run npm run build in frontend/ (output: wwwroot/app).", context.RequestAborted);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.SendFileAsync(shell, context.RequestAborted);
    }
}
