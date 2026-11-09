using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CreditScoring.Api.Contracts;

public record ClientDetailsRequest
{
    [Required, StringLength(BusinessLimits.ApplicantNameMaxLength), Display(Name = "Full name")]
    public string? FullName { get; init; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; init; }

    [StringLength(BusinessLimits.AddressMaxLength), Display(Name = "Address")]
    public string? Address { get; init; }

    [StringLength(BusinessLimits.PhoneMaxLength), RegularExpression(BusinessLimits.PhonePattern), Display(Name = "Phone")]
    public string? Phone { get; init; }

    [StringLength(BusinessLimits.EmailMaxLength), EmailAddress, Display(Name = "E-mail")]
    public string? Email { get; init; }

    [StringLength(BusinessLimits.EmployerMaxLength), Display(Name = "Employer")]
    public string? Employer { get; init; }

    public ClientContact Contact() => new(Address, Phone, Email, Employer);
}

public sealed record CreateClientRequest : ClientDetailsRequest;

public sealed record UpdateClientRequest : ClientDetailsRequest;

public sealed record ClientQuery
{
    [FromQuery(Name = "search"), StringLength(BusinessLimits.ApplicantNameMaxLength)]
    public string? Search { get; init; }

    [FromQuery(Name = "active")]
    public bool? Active { get; init; }

    [FromQuery(Name = "page"), Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [FromQuery(Name = "pageSize"), Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record ClientResponse(
    Guid Id,
    string ClientNumber,
    string FullName,
    DateOnly? DateOfBirth,
    string? Address,
    string? Phone,
    string? Email,
    string? Employer,
    Guid BranchId,
    bool IsActive,
    DateTimeOffset? AnonymisedAt,
    DateTimeOffset CreatedAt)
{
    public static ClientResponse From(Client client) => new(
        client.Id,
        client.ClientNumber,
        client.FullName,
        client.DateOfBirth,
        client.Address,
        client.Phone,
        client.Email,
        client.Employer,
        client.BranchId,
        client.IsActive,
        client.AnonymisedAt,
        client.CreatedAt);
}

public sealed record ClientSummary(
    Guid Id,
    string ClientNumber,
    string FullName,
    DateOnly? DateOfBirth,
    int ApplicationCount,
    DateTimeOffset? LastApplicationAt,
    Guid BranchId,
    bool IsActive,
    DateTimeOffset? AnonymisedAt);

public sealed record ClientDetail(
    Guid Id,
    string ClientNumber,
    string FullName,
    DateOnly? DateOfBirth,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ApplicationSummary> Applications,
    string? Address,
    string? Phone,
    string? Email,
    string? Employer,
    Guid BranchId,
    bool IsActive,
    DateTimeOffset? AnonymisedAt)
{
    public static ClientDetail From(Client client, IReadOnlyList<ApplicationSummary> applications) => new(
        client.Id,
        client.ClientNumber,
        client.FullName,
        client.DateOfBirth,
        client.CreatedAt,
        applications,
        client.Address,
        client.Phone,
        client.Email,
        client.Employer,
        client.BranchId,
        client.IsActive,
        client.AnonymisedAt);
}
