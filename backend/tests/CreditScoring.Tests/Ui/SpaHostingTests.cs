using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace CreditScoring.Tests.Ui;

public sealed class SpaHostingTests : IDisposable
{
    private readonly string webRoot = Directory.CreateTempSubdirectory("spa-test-").FullName;

    public void Dispose() => Directory.Delete(webRoot, recursive: true);

    private static async Task<(DefaultHttpContext Context, string Body)> Serve(IFileProvider files)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await SpaHosting.ServeShellAsync(context, files);

        context.Response.Body.Position = 0;
        return (context, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task The_shell_is_the_built_index_page_and_is_never_cached()
    {
        Directory.CreateDirectory(Path.Combine(webRoot, "app"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "app", "index.html"), "<!doctype html><div id=\"root\"></div>");
        using var files = new PhysicalFileProvider(webRoot);

        var (context, body) = await Serve(files);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
        Assert.Equal("no-cache", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("<!doctype html><div id=\"root\"></div>", body);
    }

    [Fact]
    public async Task Without_a_build_the_shell_says_so_instead_of_failing()
    {
        using var files = new PhysicalFileProvider(webRoot);

        var (context, body) = await Serve(files);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.StartsWith("text/plain", context.Response.ContentType);
        Assert.Contains("npm run build", body);
    }

    [Fact]
    public void The_spa_lives_under_app_and_leaves_files_and_the_api_alone()
    {
        Assert.Equal("/app", SpaHosting.BasePath);
        Assert.Equal("app/{**path:nonfile}", SpaHosting.RoutePattern);
        Assert.Equal("app/index.html", SpaHosting.ShellFile);
    }
}
