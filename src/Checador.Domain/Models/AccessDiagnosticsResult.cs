namespace Checador.Domain.Models;

public sealed class AccessDiagnosticsResult
{
    public bool IsConnected { get; init; }
    public int EmployeeCount { get; init; }
    public DateTime? LastCheckTime { get; init; }
    public string Message { get; init; } = string.Empty;
}
