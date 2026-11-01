using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CreditScoring.Api.Contracts;
using CreditScoring.Api.Reporting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CreditScoring.Api.Ui;

public abstract record SubmitResult;

public sealed record Submitted(ApplicationResponse Application) : SubmitResult;

public sealed record Invalid(IDictionary<string, string[]> Errors) : SubmitResult;

public sealed record AlreadyDecided : SubmitResult;

public sealed record ClientCreated(ClientResponse Client) : SubmitResult;

public sealed record Done : SubmitResult;

public sealed record Conflicted(string Message) : SubmitResult;

public class ApplicationsApi(HttpClient http)
{
    private const string Path = "api/applications";
    private const string ClientsPath = "api/clients";
    private const string ModelsPath = "api/models";

    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJson.Configure(options);
        return options;
    }

    public async Task<SubmitResult> SubmitAsync(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Path, request, Json, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Created => new Submitted(await ReadAsync<ApplicationResponse>(response, Json, cancellationToken)),
            HttpStatusCode.BadRequest => new Invalid(
                (await ReadAsync<ValidationProblemDetails>(response, JsonSerializerOptions.Web, cancellationToken)).Errors),
            _ => throw Unexpected(response),
        };
    }

    public async Task<SubmitResult> DecideAsync(Guid id, RecordDecisionRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{Path}/{id}/decision", request, Json, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Created => new Submitted(await ReadAsync<ApplicationResponse>(response, Json, cancellationToken)),
            HttpStatusCode.BadRequest => new Invalid(
                (await ReadAsync<ValidationProblemDetails>(response, JsonSerializerOptions.Web, cancellationToken)).Errors),
            HttpStatusCode.Conflict => new AlreadyDecided(),
            _ => throw Unexpected(response),
        };
    }

    public async Task<SubmitResult> CreateClientAsync(CreateClientRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(ClientsPath, request, Json, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Created => new ClientCreated(await ReadAsync<ClientResponse>(response, Json, cancellationToken)),
            HttpStatusCode.BadRequest => new Invalid(
                (await ReadAsync<ValidationProblemDetails>(response, JsonSerializerOptions.Web, cancellationToken)).Errors),
            _ => throw Unexpected(response),
        };
    }

    public async Task<PagedResult<ClientSummary>> ListClientsAsync(ClientQuery query, CancellationToken cancellationToken)
    {
        var uri = QueryHelpers.AddQueryString(ClientsPath, new Dictionary<string, string?>
        {
            ["search"] = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search,
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture),
        }.Where(p => p.Value is not null));
        using var response = await http.GetAsync(uri, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
            throw Unexpected(response);
        return await ReadAsync<PagedResult<ClientSummary>>(response, Json, cancellationToken);
    }

    public async Task<ClientDetail?> GetClientAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"{ClientsPath}/{id}", cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await ReadAsync<ClientDetail>(response, Json, cancellationToken),
            HttpStatusCode.NotFound => null,
            _ => throw Unexpected(response),
        };
    }

    public async Task<ApplicationResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"{Path}/{id}", cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await ReadAsync<ApplicationResponse>(response, Json, cancellationToken),
            HttpStatusCode.NotFound => null,
            _ => throw Unexpected(response),
        };
    }

    public async Task<PagedResult<ApplicationSummary>> ListAsync(HistoryQuery query, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(ListUri(query), cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
            throw Unexpected(response);
        return await ReadAsync<PagedResult<ApplicationSummary>>(response, Json, cancellationToken);
    }

    public Task<List<ScoringModelResponse>> ListModelsAsync(CancellationToken cancellationToken) =>
        GetOkAsync<List<ScoringModelResponse>>(ModelsPath, cancellationToken);

    public Task<List<ModelEventResponse>> ListModelEventsAsync(CancellationToken cancellationToken) =>
        GetOkAsync<List<ModelEventResponse>>($"{ModelsPath}/events", cancellationToken);

    public Task<List<ShadowScoreResponse>> GetShadowScoresAsync(Guid id, CancellationToken cancellationToken) =>
        GetOkAsync<List<ShadowScoreResponse>>($"{Path}/{id}/shadow-scores", cancellationToken);

    public Task<DashboardStats> GetStatsAsync(StatsQuery query, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["from"] = query.From?.ToString("O"),
            ["to"] = query.To?.ToString("O"),
        }.Where(p => p.Value is not null);
        return GetOkAsync<DashboardStats>(QueryHelpers.AddQueryString("api/stats", parameters), cancellationToken);
    }

    public async Task<List<AvailableModelResponse>?> ListAvailableModelsAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"{ModelsPath}/available", cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => await ReadAsync<List<AvailableModelResponse>>(response, Json, cancellationToken),
            HttpStatusCode.ServiceUnavailable => null,
            _ => throw Unexpected(response),
        };
    }

    public Task<SubmitResult> RegisterModelAsync(string modelId, CancellationToken cancellationToken) =>
        ChangeModelsAsync(ModelsPath, new RegisterModelRequest { ModelId = modelId }, cancellationToken);

    public Task<SubmitResult> PromoteModelAsync(string modelId, string? reason, CancellationToken cancellationToken) =>
        ChangeModelsAsync($"{ModelsPath}/{Uri.EscapeDataString(modelId)}/promote", new ChangeModelRoleRequest { Reason = reason }, cancellationToken);

    public Task<SubmitResult> RetireModelAsync(string modelId, string? reason, CancellationToken cancellationToken) =>
        ChangeModelsAsync($"{ModelsPath}/{Uri.EscapeDataString(modelId)}/retire", new ChangeModelRoleRequest { Reason = reason }, cancellationToken);

    private async Task<SubmitResult> ChangeModelsAsync<TRequest>(string uri, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(uri, request, Json, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.OK or HttpStatusCode.Created => new Done(),
            HttpStatusCode.BadRequest => new Invalid(
                (await ReadAsync<ValidationProblemDetails>(response, JsonSerializerOptions.Web, cancellationToken)).Errors),
            HttpStatusCode.Conflict => new Conflicted(
                (await ReadAsync<ProblemDetails>(response, JsonSerializerOptions.Web, cancellationToken)).Title ?? "The change conflicts with another one."),
            _ => throw Unexpected(response),
        };
    }

    private async Task<T> GetOkAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(uri, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
            throw Unexpected(response);
        return await ReadAsync<T>(response, Json, cancellationToken);
    }

    private static string ListUri(HistoryQuery query)
    {
        var parameters = new List<KeyValuePair<string, string?>>();
        void Add(string key, object? value)
        {
            if (value is not null)
                parameters.Add(new(key, Convert.ToString(value, CultureInfo.InvariantCulture)));
        }

        Add("applicantName", query.ApplicantName);
        Add("from", query.From?.ToString("O"));
        Add("to", query.To?.ToString("O"));
        Add("status", query.Status);
        Add("recommendation", query.Recommendation);
        Add("review", query.Review);
        Add("page", query.Page);
        Add("pageSize", query.PageSize);
        return QueryHelpers.AddQueryString(Path, parameters);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        JsonSerializerOptions options,
        CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(options, cancellationToken)
            ?? throw new HttpRequestException($"empty {typeof(T).Name} body from {response.RequestMessage?.RequestUri}");

    private static HttpRequestException Unexpected(HttpResponseMessage response) =>
        new($"API returned {(int)response.StatusCode} for {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}",
            null, response.StatusCode);
}
