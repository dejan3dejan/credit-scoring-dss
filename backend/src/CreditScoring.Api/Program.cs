using CreditScoring.Api.Contracts;
using CreditScoring.Api.Data;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Ml;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Credit scoring API v1"));
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
