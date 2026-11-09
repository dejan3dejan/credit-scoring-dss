namespace CreditScoring.Api.Domain;

public abstract record UserChange;

public sealed record UserChanged : UserChange;

public sealed record UserRefused(string? Field, string Code, string Message) : UserChange;

public static class UserAdministration
{
    public static UserChange Update(User target, Guid actingUserId, string displayName, string? email, UserRole role, string preferredLanguage)
    {
        if (target.Id == actingUserId && role != target.Role)
            return new UserRefused("role", "own_role", "You cannot change your own role.");

        target.Edit(displayName, email, role, preferredLanguage);
        return new UserChanged();
    }

    public static UserChange Deactivate(User target, Guid actingUserId)
    {
        if (target.Id == actingUserId)
            return new UserRefused(null, "own_account", "You cannot deactivate your own account.");

        target.Deactivate();
        return new UserChanged();
    }
}
