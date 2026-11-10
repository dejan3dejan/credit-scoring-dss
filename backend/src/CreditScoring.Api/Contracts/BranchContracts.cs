using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record CreateBranchRequest
{
    [Required, StringLength(Branch.CodeMaxLength), RegularExpression(BusinessLimits.BranchCodePattern), Display(Name = "Code")]
    public string? Code { get; init; }

    [Required, StringLength(Branch.NameMaxLength), Display(Name = "Name")]
    public string? Name { get; init; }

    [StringLength(BusinessLimits.CityMaxLength), Display(Name = "City")]
    public string? City { get; init; }
}

public sealed record UpdateBranchRequest
{
    [Required, StringLength(Branch.NameMaxLength), Display(Name = "Name")]
    public string? Name { get; init; }

    [StringLength(BusinessLimits.CityMaxLength), Display(Name = "City")]
    public string? City { get; init; }

    [Required, Display(Name = "Version")]
    public uint? Version { get; init; }
}

public sealed record BranchResponse(Guid Id, string Code, string Name, string? City, bool IsActive, DateTimeOffset CreatedAt, uint Version)
{
    public static BranchResponse From(Branch branch) =>
        new(branch.Id, branch.Code, branch.Name, branch.City, branch.IsActive, branch.CreatedAt, branch.Version);
}
