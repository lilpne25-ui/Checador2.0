namespace Checador.Api.Auth;

public static class ApiAuthConstants
{
    public const string AuthenticationScheme = "Basic";
    public const string RoleConsulta = "consulta";
    public const string RoleAdmin = "admin";
    public const string MustChangePasswordClaim = "must_change_password";
    public const string PermissionClaim = "permission";

    public const string PermissionPreviewRead = "reports.preview.read";
    public const string PermissionReportsGenerate = "reports.generate";
    public const string PermissionKpiRead = "reports.kpi.read";
    public const string PermissionAdminUsersRead = "admin.users.read";
    public const string PermissionOpsSyncRun = "ops.sync.run";
    public const string PermissionOpsSyncStatusRead = "ops.sync.status.read";
    public const string PermissionOpsLiveRead = "ops.live.read";
    public const string PermissionOpsRecentHistoryRead = "ops.recent_history.read";
    public const string PermissionOpsDeviceTimeSync = "ops.device.time.sync";
    public const string PermissionOpsAutoSyncManage = "ops.sync.autosync.manage";
    public const string PermissionAdminEmployeesRead = "admin.employees.read";
    public const string PermissionAdminEmployeesEdit = "admin.employees.edit";
    public const string PermissionAdminEmployeesDelete = "admin.employees.delete";
    public const string PermissionAdminEmployeesReassign = "admin.employees.reassign";
    public const string PermissionAdminManualPunchCreate = "admin.manual_punch.create";
    public const string PermissionAdminPervasivePreview = "admin.pervasive.preview";
    public const string PermissionAdminPervasiveCleanup = "admin.pervasive.cleanup";
    public const string PermissionAdminSchedulesManage = "admin.schedules.manage";

    public const string PolicyConsultaOrAdmin = "ConsultaOrAdmin";
    public const string PolicyAdminOnly = "AdminOnly";
    public const string PolicyPreviewRead = "PreviewRead";
    public const string PolicyReportsGenerate = "ReportsGenerate";
    public const string PolicyKpiRead = "KpiRead";
    public const string PolicyAdminUsersRead = "AdminUsersRead";
    public const string PolicyOpsSyncRun = "OpsSyncRun";
    public const string PolicyOpsSyncStatusRead = "OpsSyncStatusRead";
    public const string PolicyOpsLiveRead = "OpsLiveRead";
    public const string PolicyOpsRecentHistoryRead = "OpsRecentHistoryRead";
    public const string PolicyOpsDeviceTimeSync = "OpsDeviceTimeSync";
    public const string PolicyOpsAutoSyncManage = "OpsAutoSyncManage";
    public const string PolicyAdminEmployeesRead = "AdminEmployeesRead";
    public const string PolicyAdminEmployeesEdit = "AdminEmployeesEdit";
    public const string PolicyAdminEmployeesDelete = "AdminEmployeesDelete";
    public const string PolicyAdminEmployeesReassign = "AdminEmployeesReassign";
    public const string PolicyAdminManualPunchCreate = "AdminManualPunchCreate";
    public const string PolicyAdminPervasivePreview = "AdminPervasivePreview";
    public const string PolicyAdminPervasiveCleanup = "AdminPervasiveCleanup";
    public const string PolicyAdminSchedulesManage = "AdminSchedulesManage";
}
