using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Ml;
using CreditScoring.Api.Ui;
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

builder.Services.AddOptions<DecisionOptions>()
    .Bind(builder.Configuration.GetSection(DecisionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICreditDecisionService, CreditDecisionService>();
builder.Services.AddControllers(options =>
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
builder.Services.AddOpenApi();

builder.Services.AddOptions<UiOptions>()
    .Bind(builder.Configuration.GetSection(UiOptions.SectionName));
builder.Services.AddHttpClient<ApplicationsApi>((services, client) =>
    client.BaseAddress = services.GetRequiredService<IOptions<UiOptions>>().Value.ApiBaseUrl
        ?? SelfAddress.Choose(services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses));
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseRequestLocalization("en-GB");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Credit scoring API v1"));
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapControllers();
app.MapRazorPages();

app.Run();
