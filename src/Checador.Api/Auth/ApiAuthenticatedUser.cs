namespace Checador.Api.Auth;

public sealed record ApiAuthenticatedUser(
    int UserId,
    string Username,
    bool MustChangePassword,
    bool IsActive,
    IReadOnlyList<string> Roles);
