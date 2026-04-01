namespace Checador.Domain.Models;

public sealed class AttendanceDayResult
{
    public int UserId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateOnly Date { get; init; }

    public DateTime? Entrada { get; init; }
    public DateTime? SalidaComida { get; init; }
    public DateTime? EntradaComida { get; init; }
    public DateTime? Salida { get; init; }

    public decimal? HorasTrabajadas { get; init; }
    public decimal? HorasExtra { get; init; }
    public string Estatus { get; init; } = "OK";
    public string? Observaciones { get; init; }
}
