using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CreditScoring.Api.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Applications/New");
}
