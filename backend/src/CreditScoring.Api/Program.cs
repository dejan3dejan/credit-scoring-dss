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
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CreditScoring")
    ?? throw new InvalidOperationException("Connection string 'CreditScoring' is not configured.");

builder.Services.AddDbContext<CreditScoringDbContext>(options => options.UseNpgsql(connectionString));

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
builder.Services.AddControllers(options =>
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
builder.Services.AddProblemDetails(ApiProblems.Configure);
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieSetup.Configure);
builder.Services.AddAuthorization(Policies.Configure);
builder.Services.AddScoped<SignInService>();
builder.Services.AddHttpContextAccessor();
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
    options.Conventions.AuthorizePage("/Applications/New", Policies.CanDecide);
    options.Conventions.AuthorizePage("/Clients/New", Policies.CanDecide);
    options.Conventions.AuthorizePage("/Dashboard", Policies.CanSeeStats);
});

var app = builder.Build();

app.UseRequestLocalization("en-GB");

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seed = app.Configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
    await DevelopmentDatabase.PrepareAsync(scope.ServiceProvider.GetRequiredService<CreditScoringDbContext>(), seed.Users, TimeProvider.System.GetUtcNow());

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

app.Run();
