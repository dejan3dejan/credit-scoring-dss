using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Auth;

public static class Permissions
{
    public const string UsersManage = "users.manage";
    public const string BranchesManage = "branches.manage";
    public const string AuditRead = "audit.read";
    public const string ClientsRead = "clients.read";
    public const string ClientsWrite = "clients.write";
    public const string ApplicationsRead = "applications.read";
    public const string ApplicationsWrite = "applications.write";
    public const string ApplicationsDecide = "applications.decide";
    public const string ApplicationsAssign = "applications.assign";
    public const string ApplicationsApproveEscalated = "applications.approve-escalated";
    public const string OutcomesRecord = "outcomes.record";
    public const string StatsRead = "stats.read";
    public const string ModelsRead = "models.read";
    public const string ModelsManage = "models.manage";

    public static readonly IReadOnlyList<string> All =
    [
        UsersManage, BranchesManage, AuditRead, ClientsRead, ClientsWrite, ApplicationsRead, ApplicationsWrite,
        ApplicationsDecide, ApplicationsAssign, ApplicationsApproveEscalated, OutcomesRecord, StatsRead, ModelsRead, ModelsManage,
    ];

    private static readonly string[] Admin = [UsersManage, BranchesManage, AuditRead];

    private static readonly string[] Officer =
        [ClientsRead, ClientsWrite, ApplicationsRead, ApplicationsWrite, ApplicationsDecide, ModelsRead];

    private static readonly string[] Manager =
        [.. Officer, ApplicationsAssign, ApplicationsApproveEscalated, StatsRead];

    private static readonly string[] Risk =
        [AuditRead, ClientsRead, ApplicationsRead, OutcomesRecord, StatsRead, ModelsRead, ModelsManage];

    public static IReadOnlyList<string> For(UserRole role) => (role switch
    {
        UserRole.Admin => Admin,
        UserRole.Officer => Officer,
        UserRole.Manager => Manager,
        UserRole.Risk => Risk,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "unknown role"),
    }).Order(StringComparer.Ordinal).ToList();

    public static IReadOnlyList<UserRole> RolesWith(string permission)
    {
        if (!All.Contains(permission))
            throw new ArgumentOutOfRangeException(nameof(permission), permission, "unknown permission");
        return Enum.GetValues<UserRole>().Where(role => For(role).Contains(permission)).ToList();
    }
}
