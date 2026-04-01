using Checador.Domain.Models;

namespace Checador.Domain.Interfaces;

public interface IAttendanceDiagnostics
{
    Task<AccessDiagnosticsResult> ProbeAsync(CancellationToken cancellationToken = default);
}
