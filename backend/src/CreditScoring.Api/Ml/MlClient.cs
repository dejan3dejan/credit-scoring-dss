using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CreditScoring.Api.Ml;

public class MlClient(HttpClient http, ILogger<MlClient> logger) : IMlClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<MlScoreResult> PredictAsync(string modelId, MlPredictRequest request, CancellationToken cancellationToken)
    {
        var result = await SendAsync(modelId, request, cancellationToken);
        if (result is MlFailure failure)
        {
            logger.LogWarning(
                "ML scoring with {ModelId} failed: {ErrorCode} (HTTP {HttpStatus}) {Message} {@Details}",
                modelId, failure.ErrorCode, failure.HttpStatus, failure.Message, failure.Details);
        }
        return result;
    }

    public async Task<MlCatalogResult> ListModelsAsync(CancellationToken cancellationToken)
    {
        var (failure, status, body) = await SendAsync(() => http.GetAsync("v2/models", cancellationToken), cancellationToken);
        failure ??= status == HttpStatusCode.OK ? null : ParseError((int)status, body);
        if (failure is null && ParseCatalog(body) is { } catalog)
            return catalog;

        failure ??= Failure(MlErrorCodes.InvalidResponse, 200, $"unusable catalog body: {Truncate(body)}");
        logger.LogWarning("ML model catalog failed: {ErrorCode} (HTTP {HttpStatus}) {Message}",
            failure.ErrorCode, failure.HttpStatus, failure.Message);
        return new MlCatalogUnavailable(failure.ErrorCode, failure.Message);
    }

    private async Task<MlScoreResult> SendAsync(string modelId, MlPredictRequest request, CancellationToken cancellationToken)
    {
        var path = $"v2/models/{Uri.EscapeDataString(modelId)}/predict";
        var (failure, status, body) = await SendAsync(() => http.PostAsJsonAsync(path, request, Json, cancellationToken), cancellationToken);
        if (failure is not null)
            return failure;
        return status == HttpStatusCode.OK ? ParseScore(body) : ParseError((int)status, body);
    }

    private async Task<(MlFailure? Failure, HttpStatusCode Status, string Body)> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            return (null, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (Failure(MlErrorCodes.Timeout, null, $"no response within {http.Timeout.TotalSeconds:0.###} s"), 0, "");
        }
        catch (HttpRequestException ex)
        {
            return (Failure(MlErrorCodes.Unreachable, null, ex.Message), 0, "");
        }
    }

    private static MlCatalog? ParseCatalog(string body)
    {
        var models = TryDeserialize<CatalogBody>(body)?.Models;
        if (models is null)
            return null;

        var parsed = new List<MlModelInfo>();
        foreach (var m in models)
        {
            if (string.IsNullOrWhiteSpace(m.ModelId) || string.IsNullOrWhiteSpace(m.ModelName) || string.IsNullOrWhiteSpace(m.Dataset)
                || m.RecommendedThreshold?.Value is not { } threshold || !double.IsFinite(threshold) || threshold <= 0 || threshold >= 1
                || m.CreatedAt is not { } createdAt)
            {
                return null;
            }
            parsed.Add(new MlModelInfo(m.ModelId, m.ModelName, m.Dataset, threshold, createdAt));
        }
        return new MlCatalog(parsed);
    }

    private static MlScoreResult ParseScore(string body)
    {
        var parsed = TryDeserialize<PredictResponseBody>(body);
        if (parsed?.PBad is not { } pBad || !double.IsFinite(pBad) || pBad < 0 || pBad > 1
            || string.IsNullOrWhiteSpace(parsed.ModelId))
        {
            return Failure(MlErrorCodes.InvalidResponse, 200, $"unusable 200 body: {Truncate(body)}");
        }
        return new MlScore(pBad, parsed.ModelId);
    }

    private static MlFailure ParseError(int status, string body)
    {
        var error = TryDeserialize<ErrorEnvelope>(body)?.Error;
        if (error is null || string.IsNullOrWhiteSpace(error.Code))
        {
            return Failure(MlErrorCodes.UnexpectedStatus, status, $"no error envelope: {Truncate(body)}");
        }
        var details = (error.Details ?? [])
            .Select(d => new MlErrorDetail(d.Field ?? "", d.Issue ?? ""))
            .ToList();
        return new MlFailure(error.Code, status, error.Message ?? "", details);
    }

    private static T? TryDeserialize<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MlFailure Failure(string code, int? status, string message) => new(code, status, message, []);

    private static string Truncate(string body) => body.Length <= 200 ? body : body[..200] + "...";

    private sealed class PredictResponseBody
    {
        public string? ModelId { get; init; }
        public double? PBad { get; init; }
    }

    private sealed class CatalogBody
    {
        public List<CatalogModelBody>? Models { get; init; }
    }

    private sealed class CatalogModelBody
    {
        public string? ModelId { get; init; }
        public string? ModelName { get; init; }
        public string? Dataset { get; init; }
        public ThresholdBody? RecommendedThreshold { get; init; }
        public DateTimeOffset? CreatedAt { get; init; }
    }

    private sealed class ThresholdBody
    {
        public double? Value { get; init; }
    }

    private sealed class ErrorEnvelope
    {
        public ErrorBody? Error { get; init; }
    }

    private sealed class ErrorBody
    {
        public string? Code { get; init; }
        public string? Message { get; init; }
        public List<ErrorDetailBody>? Details { get; init; }
    }

    private sealed class ErrorDetailBody
    {
        public string? Field { get; init; }
        public string? Issue { get; init; }
    }
}
