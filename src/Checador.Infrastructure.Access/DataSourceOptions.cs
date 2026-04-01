namespace Checador.Infrastructure.Access;

public sealed class DataSourceOptions
{
    /// <summary>
    /// Valores permitidos: Sql, Direct, Access
    /// </summary>
    public string Primary { get; set; } = "Sql";
}
