using CreditScoring.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages;

public class IndexModel(IAuthorizationService authorization) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        if ((await authorization.AuthorizeAsync(User, Policies.CanDecide)).Succeeded)
            return RedirectToPage("/Applications/New");
        if ((await authorization.AuthorizeAsync(User, Policies.CanSeeStats)).Succeeded)
            return RedirectToPage("/Dashboard");
        return RedirectToPage("/Applications/Index");
    }
}
