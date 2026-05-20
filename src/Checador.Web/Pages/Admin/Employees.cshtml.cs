using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class EmployeesModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public EmployeesModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IReadOnlyList<ApiAdminEmployeeItemResponse> Employees { get; private set; } = Array.Empty<ApiAdminEmployeeItemResponse>();
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

        Employees = await _apiClient.GetAdminEmployeesAsync(ApiBasicToken, cancellationToken);
        if (Employees.Count == 0)
        {
            ErrorMessage = "No se pudieron obtener empleados API o no hay registros en SQL.";
        }

        return Page();
    }
}
