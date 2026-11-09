using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Auth;

public static class Permissions
{
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
    public const string ModelsManage = "models.manage";

    private static readonly string[] Officer =
        [ClientsRead, ClientsWrite, ApplicationsRead, ApplicationsWrite, ApplicationsDecide];

    private static readonly string[] Manager =
        [.. Officer, ApplicationsAssign, ApplicationsApproveEscalated, StatsRead];

    private static readonly string[] Risk =
        [AuditRead, ClientsRead, ApplicationsRead, OutcomesRecord, StatsRead, ModelsManage];

    public static IReadOnlyList<string> For(UserRole role) => (role switch
    {
        UserRole.Officer => Officer,
        UserRole.Manager => Manager,
        UserRole.Risk => Risk,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "unknown role"),
    }).Order(StringComparer.Ordinal).ToList();
}
