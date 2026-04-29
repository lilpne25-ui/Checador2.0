namespace Checador.Infrastructure.Access;

public sealed record SqlSyncRunResult(
    int DeviceEmployees,
    int DevicePunches,
    int InsertedPunches,
    int DuplicatePunches,
    DateTime? LastSyncTime,
    string Message);

public sealed record SqlEmployeeCatalogItem(
    int UserId,
    string DeviceName,
    string? SqlName,
    DateTime? SqlUpdatedAt,
    bool HasPhoto,
    string? Department,
    string? Position,
    string? PervasiveDept,
    string? PervasiveShift,
    int? WorkScheduleCode,
    bool OvertimeAuthorized,
    decimal StandardShiftHours);

public sealed record SqlPunchHistoryItem(
    int UserId,
    string EmployeeName,
    DateTime CheckTime,
    string Source,
    int? InOutMode = null);

public sealed record ManualPunchEntry(
    DateTime CheckTime,
    int? InOutMode);

public sealed record SqlSyncStats(
    int TotalEmployees,
    int TotalPunches,
    DateTime? LastPunch,
    DateTime? LastSync,
    string LastStatus,
    string LastMessage);

public sealed record SqlLiveStatus(
    bool DeviceConnected,
    DateTime? DeviceTime,
    int? DeviceUsers,
    int SqlEmployees,
    int SqlPunches,
    DateTime? LastPunch,
    DateTime? LastSync,
    string LastSyncStatus,
    string DeviceMessage);

public sealed record EmployeeRenameResult(
    int UserId,
    string NewName,
    bool UpdatedOnDevice,
    string DeviceMessage,
    bool UpdatedInSql);

public sealed record EmployeeDeleteResult(
    int UserId,
    bool DeletedOnDevice,
    string DeviceMessage,
    bool DeletedInSql,
    int DeletedHistoryRows);

public sealed record WorkScheduleItem(
    int Code,
    string Name,
    TimeSpan StartTime,
    TimeSpan EndTime,
    decimal StandardHours,
    bool IsActive);

public sealed record EmployeeAdminSnapshot(
    int UserId,
    string? SqlName,
    string? Department,
    string? Position,
    string? PervasiveDept,
    string? PervasiveShift,
    int? WorkScheduleCode,
    bool OvertimeAuthorized);

public sealed record PervasivePayloadPreviewItem(
    int UserId,
    string Action,
    string Employee,
    string EmployeeName,
    DateTime ChargeDate,
    decimal InTsMin,
    decimal OutTsMin,
    string DateIn,
    string TimeIn,
    string DateOut,
    string TimeOut,
    string Dept,
    string Shift,
    string ClockedIn,
    string LastChangeProgram,
    string ActualDate,
    string Notes);

public sealed record EmployeeReassignPreviewResult(
    int OldUserId,
    int NewUserId,
    DateTime FromDate,
    DateTime ToDate,
    int SqlOldRows,
    int SqlTargetRows,
    int SqlConflictRows,
    int SqlDistinctDays,
    IReadOnlyList<DateTime> SqlAffectedDates,
    bool PervasiveEnabled,
    int PervasiveOldRows,
    int PervasiveTargetRows,
    int PervasiveConflictRows,
    int PervasiveDistinctDays,
    IReadOnlyList<DateTime> PervasiveAffectedDates,
    string Message);

public sealed record EmployeeReassignApplyResult(
    int OldUserId,
    int NewUserId,
    DateTime FromDate,
    DateTime ToDate,
    int SqlUpdatedRows,
    int SqlSkippedConflicts,
    bool SqlEmployeeUpserted,
    bool PervasiveEnabled,
    int PervasiveUpdatedRows,
    int PervasiveSkippedConflicts,
    string Message);

public sealed record PervasiveNightShiftCleanupResult(
    bool Enabled,
    DateTime FromDate,
    DateTime ToDate,
    int? UserId,
    int CandidatePunches,
    int ProcessedPunches,
    bool TruncatedByTop,
    int AffectedUsers,
    int DeletedSplitRows,
    string MirrorMessage,
    string Message);
