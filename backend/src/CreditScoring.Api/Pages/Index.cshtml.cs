using CreditScoring.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages;

public class IndexModel(IAuthorizationService authorization) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        if ((await authorization.AuthorizeAsync(User, Permissions.ApplicationsWrite)).Succeeded)
            return RedirectToPage("/Applications/New");
        if ((await authorization.AuthorizeAsync(User, Permissions.StatsRead)).Succeeded)
            return RedirectToPage("/Dashboard");
        if ((await authorization.AuthorizeAsync(User, Permissions.ApplicationsRead)).Succeeded)
            return RedirectToPage("/Applications/Index");
        return Forbid();
    }
}
