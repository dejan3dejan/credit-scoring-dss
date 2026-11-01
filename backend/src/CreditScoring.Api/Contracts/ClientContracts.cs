using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public sealed record CreateClientRequest
{
    [Required, StringLength(BusinessLimits.ApplicantNameMaxLength), Display(Name = "Full name")]
    public string? FullName { get; init; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; init; }
}

public sealed record ClientQuery
{
    [FromQuery(Name = "search"), StringLength(BusinessLimits.ApplicantNameMaxLength)]
    public string? Search { get; init; }

    [FromQuery(Name = "page"), Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [FromQuery(Name = "pageSize"), Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record ClientResponse(Guid Id, string ClientNumber, string FullName, DateOnly? DateOfBirth, DateTimeOffset CreatedAt)
{
    public static ClientResponse From(Client client) =>
        new(client.Id, client.ClientNumber, client.FullName, client.DateOfBirth, client.CreatedAt);
}

public sealed record ClientSummary(
    Guid Id,
    string ClientNumber,
    string FullName,
    DateOnly? DateOfBirth,
    int ApplicationCount,
    DateTimeOffset? LastApplicationAt);

public sealed record ClientDetail(
    Guid Id,
    string ClientNumber,
    string FullName,
    DateOnly? DateOfBirth,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ApplicationSummary> Applications);
