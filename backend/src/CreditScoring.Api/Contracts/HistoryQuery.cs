using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public enum ReviewFilter
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Override = 4,
}

public sealed record HistoryQuery
{
    [FromQuery(Name = "applicantName"), StringLength(BusinessLimits.ApplicantNameMaxLength)]
    public string? ApplicantName { get; init; }

    [FromQuery(Name = "from")]
    public DateTimeOffset? From { get; init; }

    [FromQuery(Name = "to")]
    public DateTimeOffset? To { get; init; }

    [FromQuery(Name = "status"), EnumDataType(typeof(DecisionStatus))]
    public DecisionStatus? Status { get; init; }

    [FromQuery(Name = "recommendation"), EnumDataType(typeof(Recommendation))]
    public Recommendation? Recommendation { get; init; }

    [FromQuery(Name = "review"), EnumDataType(typeof(ReviewFilter))]
    public ReviewFilter? Review { get; init; }

    [FromQuery(Name = "page"), Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [FromQuery(Name = "pageSize"), Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
