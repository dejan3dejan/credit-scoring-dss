using System.ComponentModel.DataAnnotations;
using CreditScoring.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages.Account;

public class LoginModel(SignInService signIn) : PageModel
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

        switch (await signIn.SignInAsync(UserName!, Password!, cancellationToken))
        {
            case SignedIn signedIn:
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Credentials.PrincipalFor(signedIn.User));
                return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");

            case LockedOut lockedOut:
                ModelState.AddModelError("", $"This account is locked after too many wrong passwords. Try again after {lockedOut.Until:HH:mm} UTC.");
                return Page();

            case InvalidCredentials:
                ModelState.AddModelError("", "The user name or password is not correct.");
                return Page();

            case var other:
                throw new InvalidOperationException($"unhandled sign-in result {other.GetType().Name}");
        }
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage();
    }
}
