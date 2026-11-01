using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Auth;
using CreditScoring.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CreditScoring.Api.Pages.Account;

public class LoginModel(CreditScoringDbContext db) : PageModel
{
    [BindProperty, Required, Display(Name = "User name")]
    public string? UserName { get; set; }

    [BindProperty, Required, DataType(DataType.Password)]
    public string? Password { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        var name = UserName!.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == name, cancellationToken);
        if (user is null || !Credentials.Verify(user, Password!))
        {
            ModelState.AddModelError("", "The user name or password is not correct.");
            return Page();
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Credentials.PrincipalFor(user));
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage();
    }
}
