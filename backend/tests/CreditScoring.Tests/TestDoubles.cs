using System.Text.Json;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreditScoring.Tests;

internal sealed class FakeMlClient : IMlClient
{
    private readonly Lock gate = new();

    public MlScoreResult Result { get; set; } = new MlScore(0.1, TestData.ModelId);
    public Dictionary<string, MlScoreResult> ResultsByModel { get; } = [];
    public HashSet<string> Hanging { get; } = [];
    public List<string> ScoredModels { get; } = [];
    public MlPredictRequest? LastRequest { get; private set; }

    public MlCatalogResult Catalog { get; set; } = new MlCatalog([]);

    public Task<MlScoreResult> PredictAsync(string modelId, MlPredictRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ScoredModels.Add(modelId);
            LastRequest = request;
        }
        return Hanging.Contains(modelId)
            ? HangAsync(cancellationToken)
            : Task.FromResult(ResultsByModel.GetValueOrDefault(modelId, Result));
    }

    private static async Task<MlScoreResult> HangAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("an infinite delay ended without cancellation");
    }

    public Task<MlCatalogResult> ListModelsAsync(CancellationToken cancellationToken) => Task.FromResult(Catalog);
}

internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestServices
{
    public static CreditDecisionService DecisionService(IMlClient ml, TimeProvider time, int shadowTimeoutSeconds = 1) =>
        new(ml, time, Options.Create(new MlServiceOptions
        {
            BaseUrl = new Uri("http://localhost:8000/"),
            TimeoutSeconds = 5,
            ShadowTimeoutSeconds = shadowTimeoutSeconds,
        }), NullLogger<CreditDecisionService>.Instance);

    public static IServiceCollection Api() => new ServiceCollection()
        .AddLogging()
        .AddProblemDetails(ApiProblems.Configure)
        .AddControllers(ApiValidation.Configure).Services;

    public static ControllerContext SignedIn(User user) => new()
    {
        HttpContext = new DefaultHttpContext { RequestServices = Api().BuildServiceProvider(), User = Credentials.PrincipalFor(user) },
    };

    public static JsonElement FieldErrors(ValidationProblemDetails problem) =>
        JsonSerializer.SerializeToElement(problem, JsonSerializerOptions.Web).GetProperty("fieldErrors");

    public static string[] FieldCodes(ValidationProblemDetails problem, string field) =>
        FieldErrors(problem).GetProperty(field).EnumerateArray().Select(e => e.GetProperty("code").GetString()!).ToArray();
}
