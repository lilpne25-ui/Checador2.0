using System.ComponentModel.DataAnnotations;
using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages.Admin;

[Authorize(Policy = "AdminOnly")]
public sealed class SchedulesModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public SchedulesModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public ScheduleInput Input { get; set; } = new();

    public IReadOnlyList<ApiWorkScheduleResponse> Schedules { get; private set; } = Array.Empty<ApiWorkScheduleResponse>();
    public string? ErrorMessage { get; private set; }
    public string? OperationMessage { get; private set; }
    public bool OperationSuccess { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        if (Schedules.Count > 0)
        {
            var first = Schedules.OrderBy(x => x.Code).First();
            Input = new ScheduleInput
            {
                Code = first.Code,
                Name = first.Name,
                StartTime = first.StartTime.ToString(@"hh\:mm"),
                EndTime = first.EndTime.ToString(@"hh\:mm"),
                StandardHours = first.StandardHours,
                LateToleranceMinutes = first.LateToleranceMinutes,
                IsActive = first.IsActive
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var guard = await EnsureCanUseAdminAsync(cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Corrige los datos del horario antes de guardar.";
            return Page();
        }

        var request = new ApiWorkScheduleUpsertRequest
        {
            Name = Input.Name.Trim(),
            StartTime = Input.StartTime.Trim(),
            EndTime = Input.EndTime.Trim(),
            StandardHours = Input.StandardHours,
            IsActive = Input.IsActive,
            LateToleranceMinutes = Input.LateToleranceMinutes
        };

        var result = await _apiClient.UpsertWorkScheduleAsync(ApiBasicToken!, Input.Code, request, cancellationToken);
        OperationSuccess = result.Success;
        OperationMessage = result.Success
            ? "Horario guardado correctamente."
            : $"No se pudo guardar ({result.StatusCode}): {result.Message}";

        Schedules = await _apiClient.GetWorkSchedulesAsync(ApiBasicToken!, cancellationToken);
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

        Schedules = await _apiClient.GetWorkSchedulesAsync(ApiBasicToken, cancellationToken);
        return null;
    }

    public sealed class ScheduleInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "El codigo debe ser mayor que 0.")]
        [Display(Name = "Codigo horario")]
        public int Code { get; set; }

        [Required]
        [StringLength(80)]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^([01]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Usa formato HH:mm.")]
        [Display(Name = "Entrada")]
        public string StartTime { get; set; } = "07:00";

        [Required]
        [RegularExpression("^([01]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Usa formato HH:mm.")]
        [Display(Name = "Salida")]
        public string EndTime { get; set; } = "16:36";

        [Range(typeof(decimal), "0.10", "24.00", ErrorMessage = "Horas estandar fuera de rango.")]
        [Display(Name = "Horas estandar")]
        public decimal StandardHours { get; set; } = 9.60m;

        [Range(0, 180)]
        [Display(Name = "Tolerancia retardo (min)")]
        public int LateToleranceMinutes { get; set; } = 10;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }
}
