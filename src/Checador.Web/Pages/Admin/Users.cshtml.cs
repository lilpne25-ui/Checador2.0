using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class UsersModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public UsersModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<ApiAdminUserResponse> Users { get; private set; } = Array.Empty<ApiAdminUserResponse>();
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToLogin();
        }

        if (MustChangePassword)
        {
            return RedirectToPasswordChange();
        }

        if (string.IsNullOrWhiteSpace(ApiBasicToken))
        {
            return RedirectToLogin();
        }

        Users = await _apiClient.GetAdminUsersAsync(ApiBasicToken, cancellationToken);
        if (Users.Count == 0)
        {
            ErrorMessage = "No se pudieron obtener usuarios API o no hay registros.";
        }

        return Page();
    }
}