using CreditScoring.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages;

public class IndexModel(IAuthorizationService authorization) : PageModel
{
    public async Task<IActionResult> OnGetAsync() =>
        (await authorization.AuthorizeAsync(User, Policies.CanDecide)).Succeeded
            ? RedirectToPage("/Applications/New")
            : RedirectToPage("/Applications/Index");
}
