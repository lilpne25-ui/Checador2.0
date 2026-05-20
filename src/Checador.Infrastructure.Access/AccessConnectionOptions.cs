namespace Checador.Infrastructure.Access;

public sealed class AccessConnectionOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 120;
}
