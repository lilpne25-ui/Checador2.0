namespace Checador.Api.Auth;

public static class ApiPermissionCatalog
{
    private static readonly IReadOnlySet<string> ConsultaPermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ApiAuthConstants.PermissionPreviewRead,
            ApiAuthConstants.PermissionKpiRead
        };

    private static readonly IReadOnlySet<string> AdminPermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ApiAuthConstants.PermissionPreviewRead,
            ApiAuthConstants.PermissionReportsGenerate,
            ApiAuthConstants.PermissionKpiRead,
            ApiAuthConstants.PermissionAdminUsersRead,
            ApiAuthConstants.PermissionOpsSyncRun,
            ApiAuthConstants.PermissionOpsSyncStatusRead,
            ApiAuthConstants.PermissionOpsLiveRead,
            ApiAuthConstants.PermissionOpsRecentHistoryRead,
            ApiAuthConstants.PermissionOpsDeviceTimeSync,
            ApiAuthConstants.PermissionOpsAutoSyncManage,
            ApiAuthConstants.PermissionAdminEmployeesRead,
            ApiAuthConstants.PermissionAdminEmployeesEdit,
            ApiAuthConstants.PermissionAdminEmployeesDelete,
            ApiAuthConstants.PermissionAdminEmployeesReassign,
            ApiAuthConstants.PermissionAdminManualPunchCreate,
            ApiAuthConstants.PermissionAdminPervasivePreview,
            ApiAuthConstants.PermissionAdminPervasiveCleanup,
            ApiAuthConstants.PermissionAdminSchedulesManage
        };

    public static IReadOnlyList<string> ResolvePermissions(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            if (string.Equals(role, ApiAuthConstants.RoleConsulta, StringComparison.OrdinalIgnoreCase))
            {
                permissions.UnionWith(ConsultaPermissions);
                continue;
            }

            if (string.Equals(role, ApiAuthConstants.RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                permissions.UnionWith(AdminPermissions);
            }
        }

        return permissions
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
