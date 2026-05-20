using System.Security.Cryptography;
using System.Text;
using Checador.Infrastructure.Access;
using Microsoft.Data.SqlClient;

namespace Checador.Api.Auth;

public sealed class SqlApiAuthService : IApiAuthService
{
    private readonly SqlSyncOptions _sqlOptions;

    public SqlApiAuthService(SqlSyncOptions sqlOptions)
    {
        _sqlOptions = sqlOptions;
    }

    public async Task<ApiAuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedUser = username.Trim();

        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT
    u.UserId,
    u.Username,
    u.PasswordSalt,
    u.PasswordHash,
    u.MustChangePassword,
    u.IsActive,
    r.RoleName
FROM dbo.ApiUsers u
LEFT JOIN dbo.ApiUserRoles ur ON ur.UserId = u.UserId
LEFT JOIN dbo.ApiRoles r ON r.RoleId = ur.RoleId AND r.IsActive = 1
WHERE u.Username = @Username;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Username", normalizedUser);

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int? userId = null;
        string? dbUsername = null;
        bool mustChangePassword = false;
        bool isActive = false;
        byte[]? salt = null;
        byte[]? hash = null;

        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                userId ??= reader.GetInt32(0);
                dbUsername ??= reader.GetString(1);
                salt ??= await reader.IsDBNullAsync(2, cancellationToken) ? null : (byte[])reader[2];
                hash ??= await reader.IsDBNullAsync(3, cancellationToken) ? null : (byte[])reader[3];
                mustChangePassword = !await reader.IsDBNullAsync(4, cancellationToken) && reader.GetBoolean(4);
                isActive = !await reader.IsDBNullAsync(5, cancellationToken) && reader.GetBoolean(5);

                if (!await reader.IsDBNullAsync(6, cancellationToken))
                {
                    roles.Add(reader.GetString(6).Trim());
                }
            }
        }

        if (!userId.HasValue || string.IsNullOrWhiteSpace(dbUsername) || !isActive || salt is null || hash is null)
        {
            return null;
        }

        var passwordOk = VerifyPassword(password, salt, hash);
        if (!passwordOk)
        {
            return null;
        }

        await using (var updateCmd = new SqlCommand(
            "UPDATE dbo.ApiUsers SET LastLoginAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE UserId = @UserId;",
            connection))
        {
            updateCmd.Parameters.AddWithValue("@UserId", userId.Value);
            await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return new ApiAuthenticatedUser(
            userId.Value,
            dbUsername,
            mustChangePassword,
            isActive,
            roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        if (newPassword.Length < 8)
        {
            return false;
        }

        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT PasswordSalt, PasswordHash
FROM dbo.ApiUsers
WHERE UserId = @UserId
  AND IsActive = 1;";

        byte[]? currentSalt = null;
        byte[]? currentHash = null;
        await using (var cmd = new SqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                currentSalt = await reader.IsDBNullAsync(0, cancellationToken) ? null : (byte[])reader[0];
                currentHash = await reader.IsDBNullAsync(1, cancellationToken) ? null : (byte[])reader[1];
            }
        }

        if (currentSalt is null || currentHash is null || !VerifyPassword(currentPassword, currentSalt, currentHash))
        {
            return false;
        }

        var newSalt = RandomNumberGenerator.GetBytes(16);
        var newHash = ComputePasswordHash(newPassword, newSalt);

        const string updateSql = @"
UPDATE dbo.ApiUsers
SET PasswordSalt = @PasswordSalt,
    PasswordHash = @PasswordHash,
    MustChangePassword = 0,
    UpdatedAt = SYSUTCDATETIME()
WHERE UserId = @UserId;";

        await using var updateCmd = new SqlCommand(updateSql, connection);
        updateCmd.Parameters.AddWithValue("@PasswordSalt", newSalt);
        updateCmd.Parameters.AddWithValue("@PasswordHash", newHash);
        updateCmd.Parameters.AddWithValue("@UserId", userId);
        var updated = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        return updated > 0;
    }

    public async Task<IReadOnlyList<ApiUserSummary>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT
    u.UserId,
    u.Username,
    u.MustChangePassword,
    u.IsActive,
    u.LastLoginAt,
    r.RoleName
FROM dbo.ApiUsers u
LEFT JOIN dbo.ApiUserRoles ur ON ur.UserId = u.UserId
LEFT JOIN dbo.ApiRoles r ON r.RoleId = ur.RoleId AND r.IsActive = 1
ORDER BY u.UserId, r.RoleName;";

        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var byUser = new Dictionary<int, (string Username, bool MustChangePassword, bool IsActive, DateTime? LastLoginAt, HashSet<string> Roles)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var userId = reader.GetInt32(0);
            if (!byUser.TryGetValue(userId, out var data))
            {
                data = (
                    reader.GetString(1),
                    !await reader.IsDBNullAsync(2, cancellationToken) && reader.GetBoolean(2),
                    !await reader.IsDBNullAsync(3, cancellationToken) && reader.GetBoolean(3),
                    await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetDateTime(4),
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            if (!await reader.IsDBNullAsync(5, cancellationToken))
            {
                data.Roles.Add(reader.GetString(5).Trim());
            }

            byUser[userId] = data;
        }

        return byUser
            .OrderBy(x => x.Key)
            .Select(x => new ApiUserSummary(
                x.Key,
                x.Value.Username,
                x.Value.MustChangePassword,
                x.Value.IsActive,
                x.Value.LastLoginAt,
                x.Value.Roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }

    private static byte[] ComputePasswordHash(string password, byte[] salt)
    {
        var passwordBytes = Encoding.Unicode.GetBytes(password);
        var combined = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, combined, salt.Length, passwordBytes.Length);
        return SHA256.HashData(combined);
    }

    private static bool VerifyPassword(string password, byte[] salt, byte[] expectedHash)
    {
        var computed = ComputePasswordHash(password, salt);
        return CryptographicOperations.FixedTimeEquals(computed, expectedHash);
    }
}
