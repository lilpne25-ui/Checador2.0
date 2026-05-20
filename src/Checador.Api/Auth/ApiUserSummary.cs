namespace Checador.Api.Auth;

public sealed record ApiUserSummary(
    int UserId,
    string Username,
    bool MustChangePassword,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);
