using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record CreateUserRequest
{
    [Required, StringLength(BusinessLimits.UserNameMaxLength), RegularExpression(BusinessLimits.UserNamePattern), Display(Name = "User name")]
    public string? UserName { get; init; }

    [Required, StringLength(BusinessLimits.DisplayNameMaxLength), Display(Name = "Display name")]
    public string? DisplayName { get; init; }

    [StringLength(BusinessLimits.EmailMaxLength), EmailAddress, Display(Name = "E-mail")]
    public string? Email { get; init; }

    [Required, EnumDataType(typeof(UserRole)), Display(Name = "Role")]
    public UserRole? Role { get; init; }

    [Display(Name = "Branch")]
    public Guid? BranchId { get; init; }

    [AllowedValues(Languages.English, Languages.Serbian, null), Display(Name = "Language")]
    public string? PreferredLanguage { get; init; }

    [Required, StringLength(BusinessLimits.PasswordMaxLength, MinimumLength = BusinessLimits.PasswordMinLength), Display(Name = "Temporary password")]
    public string? TemporaryPassword { get; init; }
}

public sealed record UpdateUserRequest
{
    [Required, StringLength(BusinessLimits.DisplayNameMaxLength), Display(Name = "Display name")]
    public string? DisplayName { get; init; }

    [StringLength(BusinessLimits.EmailMaxLength), EmailAddress, Display(Name = "E-mail")]
    public string? Email { get; init; }

    [Required, EnumDataType(typeof(UserRole)), Display(Name = "Role")]
    public UserRole? Role { get; init; }

    [Display(Name = "Branch")]
    public Guid? BranchId { get; init; }

    [Required, AllowedValues(Languages.English, Languages.Serbian, null), Display(Name = "Language")]
    public string? PreferredLanguage { get; init; }

    [Required, Display(Name = "Version")]
    public uint? Version { get; init; }
}

public sealed record ResetPasswordRequest
{
    [Required, StringLength(BusinessLimits.PasswordMaxLength, MinimumLength = BusinessLimits.PasswordMinLength), Display(Name = "Temporary password")]
    public string? TemporaryPassword { get; init; }
}

public sealed record UserResponse(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Email,
    UserRole Role,
    BranchSummary? Branch,
    bool IsActive,
    string PreferredLanguage,
    bool MustChangePassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSignedInAt,
    DateTimeOffset? LockedUntil,
    uint Version)
{
    public static UserResponse From(User user, Branch? branch, DateTimeOffset now) => new(
        user.Id,
        user.UserName,
        user.DisplayName,
        user.Email,
        user.Role,
        BranchSummary.For(user, branch),
        user.IsActive,
        user.PreferredLanguage,
        user.MustChangePassword,
        user.CreatedAt,
        user.LastSignedInAt,
        user.IsLockedOut(now) ? user.LockedUntil : null,
        user.Version);
}
