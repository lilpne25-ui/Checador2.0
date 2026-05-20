namespace Checador.Web.Models;

public sealed class ApiLoginResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class ApiReportDay
{
    public string Date { get; set; } = string.Empty;
    public DateTime? Entrada { get; set; }
    public DateTime? SalidaComida { get; set; }
    public DateTime? EntradaComida { get; set; }
    public DateTime? Salida { get; set; }
    public string? Estatus { get; set; }
    public decimal? HorasTrabajadas { get; set; }
    public decimal? HorasExtra { get; set; }
}

public sealed class ApiPreviewEmployeeResponse
{
    public int UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal TotalWorkedHours { get; set; }
    public decimal TotalExtraHours { get; set; }
    public List<ApiReportDay> Days { get; set; } = new();
}

public sealed class ApiKpiResponse
{
    public int EmployeesProcessed { get; set; }
    public int EmployeesWithoutRecords { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal TotalExtraHours { get; set; }
}

public sealed class ApiAdminUserResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class ApiOpsSyncStatusResponse
{
    public int TotalEmployees { get; set; }
    public int TotalPunches { get; set; }
    public DateTime? LastPunch { get; set; }
    public DateTime? LastSync { get; set; }
    public string LastStatus { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiOpsLiveStatusResponse
{
    public bool DeviceConnected { get; set; }
    public DateTime? DeviceTime { get; set; }
    public int? DeviceUsers { get; set; }
    public int SqlEmployees { get; set; }
    public int SqlPunches { get; set; }
    public DateTime? LastPunch { get; set; }
    public DateTime? LastSync { get; set; }
    public string LastSyncStatus { get; set; } = string.Empty;
    public string DeviceMessage { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiOpsHistoryItem
{
    public int UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime CheckTime { get; set; }
    public string Source { get; set; } = string.Empty;
    public int? InOutMode { get; set; }
}

public sealed class ApiOpsSyncRunResponse
{
    public int DeviceEmployees { get; set; }
    public int DevicePunches { get; set; }
    public int InsertedPunches { get; set; }
    public int DuplicatePunches { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiOpsDeviceTimeSyncResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiOpsAutoSyncStatusResponse
{
    public bool Enabled { get; set; }
    public bool Busy { get; set; }
    public int IntervalMinutes { get; set; }
    public DateTimeOffset? LastRunStartedAt { get; set; }
    public DateTimeOffset? LastRunFinishedAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminEmployeeItemResponse
{
    public int UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? PervasiveDept { get; set; }
    public string? PervasiveShift { get; set; }
    public int? WorkScheduleCode { get; set; }
    public bool OvertimeAuthorized { get; set; }
    public decimal StandardShiftHours { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ApiAdminEmployeeUpdateRequest
{
    public string EmployeeName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? PervasiveDept { get; set; }
    public string? PervasiveShift { get; set; }
    public int? WorkScheduleCode { get; set; }
    public bool OvertimeAuthorized { get; set; }
}

public sealed class ApiAdminEmployeeUpdateResponse
{
    public int UserId { get; set; }
    public string NewName { get; set; } = string.Empty;
    public bool DeviceUpdated { get; set; }
    public string DeviceMessage { get; set; } = string.Empty;
    public bool SqlUpdated { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiWorkScheduleResponse
{
    public int Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal StandardHours { get; set; }
    public bool IsActive { get; set; }
    public int LateToleranceMinutes { get; set; }
}

public sealed class ApiWorkScheduleUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = "07:00";
    public string EndTime { get; set; } = "16:36";
    public decimal StandardHours { get; set; } = 9.60m;
    public bool IsActive { get; set; } = true;
    public int LateToleranceMinutes { get; set; } = 10;
}

public sealed class ApiWorkScheduleUpsertResponse
{
    public int Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public decimal StandardHours { get; set; }
    public bool IsActive { get; set; }
    public int LateToleranceMinutes { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminEmployeeDeleteResponse
{
    public int UserId { get; set; }
    public bool DeviceDeleted { get; set; }
    public string DeviceMessage { get; set; } = string.Empty;
    public bool SqlDeleted { get; set; }
    public int DeletedHistoryRows { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminManualPunchRequest
{
    public int UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime CheckTime { get; set; }
    public int? InOutMode { get; set; }
}

public sealed class ApiAdminManualPunchBatchRequest
{
    public int UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public List<ApiAdminManualPunchEntryRequest> BatchEntries { get; set; } = new();
}

public sealed class ApiAdminManualPunchEntryRequest
{
    public DateTime CheckTime { get; set; }
    public int? InOutMode { get; set; }
}

public sealed class ApiAdminManualPunchResponse
{
    public int DeviceEmployees { get; set; }
    public int DevicePunches { get; set; }
    public int InsertedPunches { get; set; }
    public int DuplicatePunches { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminPervasivePreviewResponse
{
    public int TotalRows { get; set; }
    public int ReturnedRows { get; set; }
    public int Top { get; set; }
    public List<ApiAdminPervasivePreviewItem> Items { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminPervasivePreviewItem
{
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Employee { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime ChargeDate { get; set; }
    public decimal InTsMin { get; set; }
    public decimal OutTsMin { get; set; }
    public string DateIn { get; set; } = string.Empty;
    public string TimeIn { get; set; } = string.Empty;
    public string DateOut { get; set; } = string.Empty;
    public string TimeOut { get; set; } = string.Empty;
    public string Dept { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public string ClockedIn { get; set; } = string.Empty;
    public string LastChangeProgram { get; set; } = string.Empty;
    public string ActualDate { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class ApiAdminPervasiveCleanupPreviewRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? UserId { get; set; }
}

public sealed class ApiAdminPervasiveCleanupRunRequest
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? UserId { get; set; }
    public bool ConfirmApply { get; set; }
    public string? ConfirmText { get; set; }
}

public sealed class ApiAdminPervasiveCleanupPreviewResponse
{
    public bool Enabled { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? UserId { get; set; }
    public int CandidatePunches { get; set; }
    public int ProcessablePunches { get; set; }
    public bool TruncatedByTop { get; set; }
    public int AffectedUsers { get; set; }
    public int EstimatedSplitRows { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminPervasiveCleanupRunResponse
{
    public bool Enabled { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? UserId { get; set; }
    public int CandidatePunches { get; set; }
    public int ProcessedPunches { get; set; }
    public bool TruncatedByTop { get; set; }
    public int AffectedUsers { get; set; }
    public int DeletedSplitRows { get; set; }
    public string MirrorMessage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiReportGenerateRequest
{
    public int? UserId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string OutputFormat { get; set; } = "excel";
    public string? OutputDirectory { get; set; }
    public string? TemplatePath { get; set; }
    public string? Company { get; set; }
    public bool UseSimulatedDataWhenEmpty { get; set; }
}

public sealed class ApiReportGenerateResponse
{
    public int EmployeesProcessed { get; set; }
    public int EmployeesWithoutRecords { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal TotalExtraHours { get; set; }
    public List<string> ReportPaths { get; set; } = new();
    public string OutputFormat { get; set; } = "excel";
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminEmployeeReassignPreviewRequest
{
    public int OldUserId { get; set; }
    public int NewUserId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool IncludePervasive { get; set; }
}

public sealed class ApiAdminEmployeeReassignApplyRequest
{
    public int OldUserId { get; set; }
    public int NewUserId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? NewName { get; set; }
    public string? NewDepartment { get; set; }
    public string? NewPervasiveDept { get; set; }
    public string? NewPervasiveShift { get; set; }
    public bool AllowSkipConflicts { get; set; } = true;
    public bool IncludePervasive { get; set; }
    public bool ConfirmApply { get; set; }
    public string? ConfirmText { get; set; }
}

public sealed class ApiAdminEmployeeReassignPreviewResponse
{
    public int OldUserId { get; set; }
    public int NewUserId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int SqlOldRows { get; set; }
    public int SqlTargetRows { get; set; }
    public int SqlConflictRows { get; set; }
    public int SqlDistinctDays { get; set; }
    public List<DateTime> SqlAffectedDates { get; set; } = new();
    public bool PervasiveEnabled { get; set; }
    public int PervasiveOldRows { get; set; }
    public int PervasiveTargetRows { get; set; }
    public int PervasiveConflictRows { get; set; }
    public int PervasiveDistinctDays { get; set; }
    public List<DateTime> PervasiveAffectedDates { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiAdminEmployeeReassignApplyResponse
{
    public int OldUserId { get; set; }
    public int NewUserId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int SqlUpdatedRows { get; set; }
    public int SqlSkippedConflicts { get; set; }
    public bool SqlEmployeeUpserted { get; set; }
    public bool PervasiveEnabled { get; set; }
    public int PervasiveUpdatedRows { get; set; }
    public int PervasiveSkippedConflicts { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiOvertimeFormatGenerateRequest
{
    public int UserId { get; set; }
    public string? EmployeeName { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string WorkDescription { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? WorkingDays { get; set; }
    public string? WorkSchedule { get; set; }
    public string ImmediateBossName { get; set; } = string.Empty;
    public string? ImmediateBossSignatureName { get; set; }
    public string? EmployeeSignatureName { get; set; }
    public string? Notes { get; set; }
    public string? OutputDirectory { get; set; }
    public string? FileName { get; set; }
    public bool UseSimulatedDataWhenEmpty { get; set; }
}

public sealed class ApiOvertimeFormatGenerateResponse
{
    public int UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiLatenessReportGenerateRequest
{
    public int? UserId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? OutputDirectory { get; set; }
    public string? FileName { get; set; }
    public bool UseSimulatedDataWhenEmpty { get; set; }
}

public sealed class ApiLatenessReportGenerateResponse
{
    public int? UserId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int IncidentsCount { get; set; }
    public int AffectedEmployees { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class ApiClientCallResult<T>
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}
