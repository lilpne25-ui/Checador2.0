using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Checador.Web.Models;
using Checador.Web.Security;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Checador.Web.Pages;

public sealed class LoginModel : PageModel
{
    private readonly ChecadorApiClient _apiClient;

    public LoginModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        await Task.CompletedTask;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApiLoginResponse? login;
        try
        {
            login = await _apiClient.LoginAsync(Input.Username, Input.Password, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = _apiClient.BuildApiUnavailableMessage();
            return Page();
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = _apiClient.BuildApiUnavailableMessage();
            return Page();
        }

        if (login is null)
        {
            ErrorMessage = "Credenciales invalidas.";
            return Page();
        }

        var basicToken = ChecadorApiClient.BuildBasicToken(Input.Username, Input.Password);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, login.UserId.ToString()),
            new(ClaimTypes.Name, login.Username),
            new(WebClaimTypes.ApiBasicToken, basicToken),
            new(WebClaimTypes.MustChangePassword, login.MustChangePassword ? "true" : "false")
        };

        foreach (var role in login.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (login.MustChangePassword)
        {
            return RedirectToPage("/ChangePassword");
        }

        return RedirectToPage("/Index");
    }

    public sealed class LoginInput
    {
        [Required]
        [Display(Name = "Usuario")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
