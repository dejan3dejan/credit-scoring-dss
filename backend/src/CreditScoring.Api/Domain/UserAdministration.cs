namespace CreditScoring.Api.Domain;

public static class UserAdministration
{
    public static Refused? CheckBranch(UserRole role, Branch? branch)
    {
        var needsBranch = role is UserRole.Officer or UserRole.Manager;
        if (needsBranch && branch is null)
            return new Refused("branchId", "branch_required", "Officers and managers belong to a branch.");
        if (!needsBranch && branch is not null)
            return new Refused("branchId", "branch_not_allowed", "Admin and Risk users do not belong to a branch.");
        if (branch is { IsActive: false })
            return new Refused("branchId", "branch_inactive", "This branch is closed.");
        return null;
    }

    public static ChangeResult Update(
        User target,
        Guid actingUserId,
        string displayName,
        string? email,
        UserRole role,
        string preferredLanguage,
        Branch? branch)
    {
        if (target.Id == actingUserId && role != target.Role)
            return new Refused("role", "own_role", "You cannot change your own role.");

        var unchangedBranch = branch is not null && branch.Id == target.BranchId;
        if (CheckBranch(role, branch) is { } refused && !(unchangedBranch && refused.Code == "branch_inactive"))
            return refused;

        target.Edit(displayName, email, role, preferredLanguage, branch?.Id);
        return new Changed();
    }

    public static ChangeResult Deactivate(User target, Guid actingUserId)
    {
        if (target.Id == actingUserId)
            return new Refused(null, "own_account", "You cannot deactivate your own account.");

        target.Deactivate();
        return new Changed();
    }

    public static ChangeResult Reactivate(User target, Branch? branch)
    {
        if (branch is { IsActive: false })
            return new Refused(null, "branch_inactive", "The user's branch is closed. Move the user to another branch first.");

        target.Reactivate();
        return new Changed();
    }
}
