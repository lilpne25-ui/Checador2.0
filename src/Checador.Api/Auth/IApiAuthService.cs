namespace Checador.Api.Auth;

public interface IApiAuthService
{
    Task<ApiAuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiUserSummary>> GetUsersAsync(CancellationToken cancellationToken = default);
}
