using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Checador.Api.Auth;

public sealed class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiAuthService _authService;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiAuthService authService)
        : base(options, logger, encoder)
    {
        _authService = authService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var raw = authorizationHeader.ToString();
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Authorization header invalido.");
        }

        var encodedCredentials = raw["Basic ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(encodedCredentials))
        {
            return AuthenticateResult.Fail("Credenciales vacias.");
        }

        string decodedCredentials;
        try
        {
            decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
        }
        catch
        {
            return AuthenticateResult.Fail("Credenciales Basic malformadas.");
        }

        var separatorIndex = decodedCredentials.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == decodedCredentials.Length - 1)
        {
            return AuthenticateResult.Fail("Formato esperado: usuario:password.");
        }

        var username = decodedCredentials[..separatorIndex];
        var password = decodedCredentials[(separatorIndex + 1)..];

        var user = await _authService.ValidateCredentialsAsync(username, password, Context.RequestAborted);
        if (user is null)
        {
            return AuthenticateResult.Fail("Usuario o password invalido.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ApiAuthConstants.MustChangePasswordClaim, user.MustChangePassword ? "true" : "false")
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var permissions = ApiPermissionCatalog.ResolvePermissions(user.Roles);
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(ApiAuthConstants.PermissionClaim, permission));
        }

        var identity = new ClaimsIdentity(claims, ApiAuthConstants.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiAuthConstants.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = "Basic realm=\"Checador LAN API\"";
        return base.HandleChallengeAsync(properties);
    }
}
