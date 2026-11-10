using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public sealed record DraftResponse(
    Guid Id,
    Guid ClientId,
    string ClientNumber,
    string ClientName,
    CreateApplicationRequest Answers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DraftStatus Status,
    Guid? ApplicationId,
    Dictionary<string, List<Dictionary<string, object>>> FieldErrors)
{
    public static DraftResponse From(ApplicationDraft draft, OpenApplication? open, DateTimeOffset now)
    {
        var client = draft.Client ?? throw new InvalidOperationException("the draft's client must be loaded to build a response");
        var answers = JsonSerializer.Deserialize<CreateApplicationRequest>(draft.Answers, ApiJson.Options)
            ?? throw new InvalidOperationException("a draft holds an answers object");
        return new DraftResponse(
            draft.Id,
            draft.ClientId,
            client.ClientNumber,
            client.FullName,
            answers,
            draft.CreatedAt,
            draft.UpdatedAt,
            draft.Status,
            draft.ApplicationId,
            draft.Status == DraftStatus.Open ? ApplicationChecks.AsFieldErrors(ApplicationChecks.All(answers, client, open, now)) : []);
    }
}

public sealed record MyWorkCounts(
    int Drafts,
    int AwaitingDecision,
    int ScoringUnavailable,
    int AwaitingSignOff,
    int DecidedLast7Days,
    int SubmittedLast7Days,
    int ApprovedLast7Days,
    int OverridesLast7Days);

public sealed record MyWorkResponse(
    MyWorkCounts Counts,
    IReadOnlyList<DraftResponse> Drafts,
    IReadOnlyList<ApplicationSummary> AwaitingDecision,
    IReadOnlyList<ApplicationSummary> ScoringUnavailable,
    IReadOnlyList<ApplicationSummary> AwaitingSignOff,
    IReadOnlyList<ApplicationSummary> RecentlyDecided);

public sealed record TeamWorkCounts(int Unassigned, int AwaitingSignOff, int ScoringUnavailable);

public sealed record TeamMember(Guid Id, string DisplayName, UserRole Role, int Open, int AwaitingSignOff);

public sealed record TeamWorkResponse(
    TeamWorkCounts Counts,
    IReadOnlyList<ApplicationSummary> Unassigned,
    IReadOnlyList<ApplicationSummary> AwaitingSignOff,
    IReadOnlyList<TeamMember> Officers);

public sealed record SearchQuery
{
    [FromQuery(Name = "q"), Required, StringLength(BusinessLimits.ApplicantNameMaxLength, MinimumLength = 2)]
    public string? Q { get; init; }
}

public sealed record SearchResponse(IReadOnlyList<ClientSummary> Clients, IReadOnlyList<ApplicationSummary> Applications);
