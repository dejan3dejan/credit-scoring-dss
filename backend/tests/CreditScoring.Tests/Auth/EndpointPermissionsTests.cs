using System.Reflection;
using CreditScoring.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CreditScoring.Tests.Auth;

public class EndpointPermissionsTests
{
    private const string Anonymous = "anonymous";
    private const string SignedIn = "signed-in";

    private static readonly Dictionary<string, string> Expected = new()
    {
        ["Applications.Create"] = "applications.write",
        ["Applications.Get"] = "applications.read",
        ["Applications.List"] = "applications.read",
        ["Applications.Rescore"] = "applications.write",
        ["Applications.ShadowScores"] = "stats.read",
        ["Applications.Decide"] = "applications.decide",
        ["Applications.SignOff"] = "applications.approve-escalated",
        ["Applications.Assign"] = "applications.assign",
        ["Applications.RecordOutcome"] = "outcomes.record",
        ["Applications.Withdraw"] = "applications.write",
        ["Applications.Notes"] = "applications.read",
        ["Applications.AddNote"] = "applications.write",
        ["Audit.List"] = "audit.read",
        ["Auth.Login"] = Anonymous,
        ["Auth.Logout"] = Anonymous,
        ["Auth.Me"] = SignedIn,
        ["Auth.ChangePassword"] = SignedIn,
        ["Auth.SetLanguage"] = SignedIn,
        ["Branches.List"] = SignedIn,
        ["Branches.Get"] = SignedIn,
        ["Branches.Create"] = "branches.manage",
        ["Branches.Update"] = "branches.manage",
        ["Branches.Deactivate"] = "branches.manage",
        ["Branches.Reactivate"] = "branches.manage",
        ["Drafts.List"] = "applications.write",
        ["Drafts.Get"] = "applications.write",
        ["Drafts.Create"] = "applications.write",
        ["Drafts.Update"] = "applications.write",
        ["Drafts.Discard"] = "applications.write",
        ["Work.Mine"] = "applications.write",
        ["Work.Team"] = "applications.assign",
        ["Search.Get"] = "clients.read",
        ["Clients.Create"] = "clients.write",
        ["Clients.Update"] = "clients.write",
        ["Clients.Deactivate"] = "clients.write",
        ["Clients.Reactivate"] = "clients.write",
        ["Clients.Anonymise"] = "clients.anonymise",
        ["Clients.Get"] = "clients.read",
        ["Clients.List"] = "clients.read",
        ["Models.List"] = "models.read",
        ["Models.Available"] = "models.manage",
        ["Models.Register"] = "models.manage",
        ["Models.Promote"] = "models.manage",
        ["Models.Retire"] = "models.manage",
        ["Models.Events"] = "models.manage",
        ["Stats.Get"] = "stats.read",
        ["Users.List"] = "users.manage",
        ["Users.Get"] = "users.manage",
        ["Users.Create"] = "users.manage",
        ["Users.Update"] = "users.manage",
        ["Users.Deactivate"] = "users.manage",
        ["Users.Reactivate"] = "users.manage",
        ["Users.ResetPassword"] = "users.manage",
    };

    private static Dictionary<string, string> Actual()
    {
        var actual = new Dictionary<string, string>();
        var controllers = typeof(AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);
        foreach (var controller in controllers)
        {
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()))
            {
                var name = $"{controller.Name.Replace("Controller", "")}.{action.Name}";
                var anonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() ?? controller.GetCustomAttribute<AllowAnonymousAttribute>();
                var policies = controller.GetCustomAttributes<AuthorizeAttribute>()
                    .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
                    .Select(a => a.Policy)
                    .OfType<string>()
                    .ToList();
                Assert.True(policies.Count <= 1, $"{name} combines policies {string.Join(", ", policies)}");
                actual[name] = anonymous is not null ? Anonymous : policies.SingleOrDefault() ?? SignedIn;
            }
        }
        return actual;
    }

    [Fact]
    public void Every_endpoint_checks_the_permission_in_the_contract()
    {
        Assert.Equal(Expected.OrderBy(e => e.Key), Actual().OrderBy(e => e.Key));
    }
}
