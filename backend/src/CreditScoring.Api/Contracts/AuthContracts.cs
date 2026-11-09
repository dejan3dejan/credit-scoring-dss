using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record LoginRequest
{
    [Required, StringLength(BusinessLimits.UserNameMaxLength), Display(Name = "User name")]
    public string? UserName { get; init; }

    [Required, StringLength(BusinessLimits.PasswordMaxLength)]
    public string? Password { get; init; }
}

public sealed record ChangePasswordRequest
{
    [Required, StringLength(BusinessLimits.PasswordMaxLength), Display(Name = "Current password")]
    public string? CurrentPassword { get; init; }

    [Required, StringLength(BusinessLimits.PasswordMaxLength, MinimumLength = BusinessLimits.PasswordMinLength), Display(Name = "New password")]
    public string? NewPassword { get; init; }
}

public sealed record LanguageRequest
{
    [Required, AllowedValues(Languages.English, Languages.Serbian, null), Display(Name = "Language")]
    public string? Language { get; init; }
}

public sealed record BranchSummary(Guid Id, string Code, string Name);

public sealed record MeResponse(
    Guid Id,
    string UserName,
    string DisplayName,
    UserRole Role,
    BranchSummary? Branch,
    IReadOnlyList<string> Permissions,
    string PreferredLanguage,
    bool MustChangePassword)
{
    public static MeResponse From(User user) => new(
        user.Id,
        user.UserName,
        user.DisplayName,
        user.Role,
        null,
        Auth.Permissions.For(user.Role),
        user.PreferredLanguage,
        user.MustChangePassword);
}
