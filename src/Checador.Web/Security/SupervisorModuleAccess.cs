using System.Security.Claims;

namespace Checador.Web.Security;

public static class SupervisorModuleAccess
{
    public const string SupervisorUsername = "SUPERVSR";

    public static bool CanAccessRestrictedModules(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return string.Equals(user.Identity?.Name, SupervisorUsername, StringComparison.OrdinalIgnoreCase);
    }
}
