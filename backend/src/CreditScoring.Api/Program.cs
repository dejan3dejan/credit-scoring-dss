using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Ml;
using CreditScoring.Api.Ui;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
SpaHosting.Map(app);

app.Run();
