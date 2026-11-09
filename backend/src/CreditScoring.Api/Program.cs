using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Ml;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CreditScoring")
    ?? throw new InvalidOperationException("Connection string 'CreditScoring' is not configured.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContext, HttpAuditContext>();
builder.Services.AddScoped<IBranchScope, HttpBranchScope>();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddDbContext<CreditScoringDbContext>((services, options) => options
    .UseNpgsql(connectionString)
    .AddInterceptors(services.GetRequiredService<AuditInterceptor>()));

builder.Services.AddOptions<MlServiceOptions>()
    .Bind(builder.Configuration.GetSection(MlServiceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<IMlClient, MlClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<MlServiceOptions>>().Value;
    client.BaseAddress = options.BaseUrl;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICreditDecisionService, CreditDecisionService>();
builder.Services.AddControllers(ApiValidation.Configure)
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
builder.Services.AddProblemDetails(ApiProblems.Configure);
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieSetup.Configure);
builder.Services.AddAuthorization(Policies.Configure);
builder.Services.AddScoped<SignInService>();
builder.Services.AddTransient<CookieForwardingHandler>();

builder.Services.AddOptions<UiOptions>()
    .Bind(builder.Configuration.GetSection(UiOptions.SectionName));
builder.Services.AddHttpClient<ApplicationsApi>((services, client) =>
    client.BaseAddress = services.GetRequiredService<IOptions<UiOptions>>().Value.ApiBaseUrl
        ?? SelfAddress.Choose(services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses))
    .AddHttpMessageHandler<CookieForwardingHandler>();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizeFolder("/Applications", Permissions.ApplicationsRead);
    options.Conventions.AuthorizeFolder("/Clients", Permissions.ClientsRead);
    options.Conventions.AuthorizePage("/Model", Permissions.ModelsRead);
    options.Conventions.AuthorizePage("/Applications/New", Permissions.ApplicationsWrite);
    options.Conventions.AuthorizePage("/Clients/New", Permissions.ClientsWrite);
    options.Conventions.AuthorizePage("/Dashboard", Permissions.StatsRead);
});

var app = builder.Build();

app.UseRequestLocalization("en-GB");

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seed = app.Configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
    await DevelopmentDatabase.PrepareAsync(scope.ServiceProvider.GetRequiredService<CreditScoringDbContext>(), seed, TimeProvider.System.GetUtcNow());

    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Credit scoring API v1"));
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();
SpaHosting.Map(app);

app.Run();
