using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class EmployeeEditModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public EmployeeEditModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty(SupportsGet = true)]
    public int UserId { get; set; }

    [BindProperty]
    public EmployeeEditInput Input { get; set; } = new();

    [BindProperty]
    [Display(Name = "Eliminar tambien historial")]
    public bool DeleteHistoryRows { get; set; }

    public string? ErrorMessage { get; private set; }
    public string? OperationMessage { get; private set; }
    public bool OperationSuccess { get; private set; }
    public IReadOnlyList<ApiWorkScheduleResponse> WorkSchedules { get; private set; } = Array.Empty<ApiWorkScheduleResponse>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        if (UserId <= 0)
        {
            return RedirectToPage("/Admin/Employees");
        }

        await LoadWorkSchedulesAsync(cancellationToken);

        var employee = await _apiClient.GetAdminEmployeeAsync(ApiBasicToken!, UserId, cancellationToken);
        if (employee is null)
        {
            ErrorMessage = "No se encontro el empleado solicitado.";
            return Page();
        }

        Input = new EmployeeEditInput
        {
            EmployeeName = employee.EmployeeName,
            Department = employee.Department,
            Position = employee.Position,
            PervasiveDept = employee.PervasiveDept,
            PervasiveShift = employee.PervasiveShift,
            WorkScheduleCode = employee.WorkScheduleCode,
            OvertimeAuthorized = employee.OvertimeAuthorized
        };

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        if (UserId <= 0)
        {
            return RedirectToPage("/Admin/Employees");
        }

        await LoadWorkSchedulesAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Corrige los datos marcados antes de guardar.";
            return Page();
        }

        var request = new ApiAdminEmployeeUpdateRequest
        {
            EmployeeName = Input.EmployeeName.Trim(),
            Department = Input.Department,
            Position = Input.Position,
            PervasiveDept = Input.PervasiveDept,
            PervasiveShift = Input.PervasiveShift,
            WorkScheduleCode = Input.WorkScheduleCode,
            OvertimeAuthorized = Input.OvertimeAuthorized
        };

        var result = await _apiClient.UpdateAdminEmployeeAsync(ApiBasicToken!, UserId, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.DeviceMessage ?? "Empleado actualizado.")
            : $"No se pudo actualizar ({result.StatusCode}): {result.Message}";

        if (result.Success)
        {
            return RedirectToPage("/Admin/Employees");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        var guard = EnsureCanUseAdmin();
        if (guard is not null)
        {
            return guard;
        }

        if (UserId <= 0)
        {
            return RedirectToPage("/Admin/Employees");
        }

        var result = await _apiClient.DeleteAdminEmployeeAsync(ApiBasicToken!, UserId, DeleteHistoryRows, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.DeviceMessage ?? "Empleado eliminado.")
            : $"No se pudo eliminar ({result.StatusCode}): {result.Message}";

        if (result.Success)
        {
            return RedirectToPage("/Admin/Employees");
        }

        return Page();
    }

    private IActionResult? EnsureCanUseAdmin()
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

        return null;
    }

    private async Task LoadWorkSchedulesAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ApiBasicToken))
        {
            WorkSchedules = Array.Empty<ApiWorkScheduleResponse>();
            return;
        }

        WorkSchedules = await _apiClient.GetWorkSchedulesAsync(ApiBasicToken, cancellationToken);
    }

    public sealed class EmployeeEditInput
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(128)]
        [Display(Name = "Nombre")]
        public string EmployeeName { get; set; } = string.Empty;

        [StringLength(64)]
        [Display(Name = "Departamento")]
        public string? Department { get; set; }

        [StringLength(64)]
        [Display(Name = "Puesto")]
        public string? Position { get; set; }

        [StringLength(64)]
        [Display(Name = "Depto Pervasive")]
        public string? PervasiveDept { get; set; }

        [StringLength(64)]
        [Display(Name = "Turno Pervasive")]
        public string? PervasiveShift { get; set; }

        [Range(1, int.MaxValue)]
        [Display(Name = "Horario")]
        public int? WorkScheduleCode { get; set; }

        [Display(Name = "Autoriza horas extra")]
        public bool OvertimeAuthorized { get; set; }
    }
}
