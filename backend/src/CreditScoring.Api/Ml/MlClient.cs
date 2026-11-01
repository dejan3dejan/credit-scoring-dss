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

    public async Task<MlScoreResult> PredictAsync(MlPredictRequest request, CancellationToken cancellationToken)
    {
        var result = await SendAsync(request, cancellationToken);
        if (result is MlFailure failure)
        {
            logger.LogWarning(
                "ML scoring failed: {ErrorCode} (HTTP {HttpStatus}) {Message} {@Details}",
                failure.ErrorCode, failure.HttpStatus, failure.Message, failure.Details);
        }
        return result;
    }

    private async Task<MlScoreResult> SendAsync(MlPredictRequest request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        string body;
        try
        {
            response = await http.PostAsJsonAsync("v1/predict", request, Json, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(MlErrorCodes.Timeout, null, $"no response within {http.Timeout.TotalSeconds:0.###} s");
        }
        catch (HttpRequestException ex)
        {
            return Failure(MlErrorCodes.Unreachable, null, ex.Message);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            return response.StatusCode == HttpStatusCode.OK ? ParseScore(body) : ParseError(status, body);
        }
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

    private static MlScoreResult ParseError(int status, string body)
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
