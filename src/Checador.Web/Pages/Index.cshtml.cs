using Checador.Web.Models;
using Checador.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Checador.Web.Pages;

public sealed class IndexModel : SecurePageModel
{
    private readonly ChecadorApiClient _apiClient;

    public IndexModel(ChecadorApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public new bool MustChangePassword { get; private set; }
    public ApiOpsSyncStatusResponse? SyncStatus { get; private set; }
    public ApiOpsLiveStatusResponse? LiveStatus { get; private set; }
    public string? DashboardMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToLogin();
        }

        MustChangePassword = base.MustChangePassword;

        if (MustChangePassword || string.IsNullOrWhiteSpace(ApiBasicToken) || User?.IsInRole("admin") != true)
        {
            return Page();
        }

        try
        {
            SyncStatus = await _apiClient.GetOpsSyncStatusAsync(ApiBasicToken, cancellationToken);
            LiveStatus = await _apiClient.GetOpsLiveStatusAsync(ApiBasicToken, cancellationToken);
        }
        catch (HttpRequestException)
        {
            DashboardMessage = _apiClient.BuildApiUnavailableMessage();
        }
        catch (TaskCanceledException)
        {
            DashboardMessage = _apiClient.BuildApiUnavailableMessage();
        }

        return Page();
    }
}
