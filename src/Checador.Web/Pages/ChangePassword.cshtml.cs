using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Checador.Web.Security;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Checador.Web.Pages;

public sealed class ChangePasswordModel : PageModel
{
    private readonly ChecadorApiClient _apiClient;

    public ChangePasswordModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public ChangePasswordInput Input { get; set; } = new();

    public string? Message { get; private set; }
    public bool Success { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var basicToken = User.FindFirstValue(WebClaimTypes.ApiBasicToken);
        if (string.IsNullOrWhiteSpace(basicToken))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Login");
        }

        var result = await _apiClient.ChangePasswordAsync(basicToken, Input.CurrentPassword, Input.NewPassword);
        Success = result.Success;
        Message = result.Message;

        if (!result.Success)
        {
            return Page();
        }

        var claims = User.Claims
            .Where(c => c.Type != WebClaimTypes.ApiBasicToken && c.Type != WebClaimTypes.MustChangePassword)
            .ToList();

        var username = User.Identity?.Name ?? string.Empty;
        claims.Add(new Claim(WebClaimTypes.ApiBasicToken, ChecadorApiClient.BuildBasicToken(username, Input.NewPassword)));
        claims.Add(new Claim(WebClaimTypes.MustChangePassword, "false"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }

    public sealed class ChangePasswordInput
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password actual")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password nueva")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
