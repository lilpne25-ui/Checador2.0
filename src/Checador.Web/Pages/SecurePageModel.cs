using System.Security.Claims;
using Checador.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Checador.Web.Pages;

public abstract class SecurePageModel : PageModel
{
    protected string? ApiBasicToken => User.FindFirstValue(WebClaimTypes.ApiBasicToken);
    protected bool MustChangePassword =>
        string.Equals(User.FindFirstValue(WebClaimTypes.MustChangePassword), "true", StringComparison.OrdinalIgnoreCase);

    protected IActionResult RedirectToLogin() => RedirectToPage("/Login");
    protected IActionResult RedirectToPasswordChange() => RedirectToPage("/ChangePassword");
}
