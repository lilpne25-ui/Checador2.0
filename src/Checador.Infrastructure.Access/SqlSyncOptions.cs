namespace Checador.Infrastructure.Access;

public sealed class SqlSyncOptions
{
    public string ConnectionString { get; set; } = "Server=.;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;";
    public int InitialLookbackDays { get; set; } = 365;
    public int AutoSyncMinutes { get; set; } = 15;
    public bool ForceLookbackWindow { get; set; } = false;
}
