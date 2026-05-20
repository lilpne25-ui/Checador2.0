using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "RestrictedModulesOnly")]
public sealed class ManualPunchModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public ManualPunchModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public ManualPunchInput Input { get; set; } = new();

    public IReadOnlyList<ApiAdminEmployeeItemResponse> Employees { get; private set; } = Array.Empty<ApiAdminEmployeeItemResponse>();
    public string? ErrorMessage { get; private set; }
    public string? OperationMessage { get; private set; }
    public bool OperationSuccess { get; private set; }
    public ApiAdminManualPunchResponse? LastResult { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        Input.CheckTime = DateTime.Now;
        Input.UseBatch = false;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Corrige los datos antes de registrar la asistencia manual.";
            return Page();
        }

        var employee = Employees.FirstOrDefault(x => x.UserId == Input.UserId);
        if (employee is null)
        {
            ErrorMessage = "No se encontró el empleado seleccionado.";
            return Page();
        }

        ApiClientCallResult<ApiAdminManualPunchResponse> result;
        if (Input.UseBatch)
        {
            var parsedEntries = ParseBatchEntries(Input.BatchEntriesText, out var parseErrors);
            if (parseErrors.Count > 0)
            {
                ErrorMessage = string.Join(" ", parseErrors);
                return Page();
            }

            var request = new ApiAdminManualPunchBatchRequest
            {
                UserId = employee.UserId,
                EmployeeName = employee.EmployeeName,
                BatchEntries = parsedEntries
            };

            result = await _apiClient.CreateAdminManualPunchBatchAsync(ApiBasicToken!, request, cancellationToken);
        }
        else
        {
            if (!Input.CheckTime.HasValue)
            {
                ErrorMessage = "La fecha/hora es obligatoria cuando no usas lote.";
                return Page();
            }

            var request = new ApiAdminManualPunchRequest
            {
                UserId = employee.UserId,
                EmployeeName = employee.EmployeeName,
                CheckTime = DateTime.SpecifyKind(Input.CheckTime.Value, DateTimeKind.Local),
                InOutMode = Input.InOutMode
            };

            result = await _apiClient.CreateAdminManualPunchAsync(ApiBasicToken!, request, cancellationToken);
        }

        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? (result.Data?.Message ?? "Asistencia manual registrada.")
            : $"No se pudo registrar ({result.StatusCode}): {result.Message}";
        LastResult = result.Data;

        return Page();
    }

    private async Task<IActionResult?> EnsureCanUseAdminAsync(CancellationToken cancellationToken)
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
        return null;
    }

    private static List<ApiAdminManualPunchEntryRequest> ParseBatchEntries(string? raw, out List<string> errors)
    {
        errors = new List<string>();
        var entries = new List<ApiAdminManualPunchEntryRequest>();

        var lines = (raw ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            errors.Add("Debes capturar al menos una marca en BatchEntries.");
            return entries;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var parts = line.Split('|', StringSplitOptions.TrimEntries);

            if (!DateTime.TryParse(parts[0], out var checkTime))
            {
                errors.Add($"Línea {i + 1}: fecha/hora inválida ('{parts[0]}').");
                continue;
            }

            int? mode = null;
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                if (!int.TryParse(parts[1], out var parsedMode) || parsedMode is < 0 or > 3)
                {
                    errors.Add($"Línea {i + 1}: modo inválido ('{parts[1]}'). Usa 0,1,2,3.");
                    continue;
                }

                mode = parsedMode;
            }

            entries.Add(new ApiAdminManualPunchEntryRequest
            {
                CheckTime = DateTime.SpecifyKind(checkTime, DateTimeKind.Local),
                InOutMode = mode
            });
        }

        if (entries.Count > 300)
        {
            errors.Add("El lote supera el máximo de 300 marcas por envío.");
        }

        return entries;
    }

    public sealed class ManualPunchInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Selecciona un empleado válido.")]
        [Display(Name = "Empleado")]
        public int UserId { get; set; }

        [Display(Name = "Fecha y hora")]
        public DateTime? CheckTime { get; set; }

        [Display(Name = "Tipo de marca")]
        [Range(0, 3, ErrorMessage = "InOutMode debe ser 0, 1, 2 o 3.")]
        public int? InOutMode { get; set; }

        [Display(Name = "Usar lote")]
        public bool UseBatch { get; set; }

        [Display(Name = "Marcas por lote")]
        public string? BatchEntriesText { get; set; }
    }
}
