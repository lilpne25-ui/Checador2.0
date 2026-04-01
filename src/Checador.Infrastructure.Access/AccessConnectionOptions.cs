namespace Checador.Infrastructure.Access;

public sealed class AccessConnectionOptions
{
    public string ConnectionString { get; set; } =
        @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=\\ociserver\INNOVAX\AREA DE TRABAJO\C.- RECURSOS HUMANOS\att2000.mdb;Mode=Share Deny None;Persist Security Info=False;";

    public int CommandTimeoutSeconds { get; set; } = 120;
}
