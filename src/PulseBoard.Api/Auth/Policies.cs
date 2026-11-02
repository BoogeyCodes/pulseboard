using PulseBoard.Api.Domain;

namespace PulseBoard.Api.Auth;

/// <summary>
/// Named authorization policies. Endpoints reference these instead of raw role strings so the
/// mapping from "what you can do" to "which roles" lives in one place.
/// </summary>
public static class Policies
{
    /// <summary>Any signed-in user. Read-only access to devices, readings and alerts.</summary>
    public const string CanView = "CanView";

    /// <summary>Operators and admins. Acknowledge and resolve alerts.</summary>
    public const string CanOperate = "CanOperate";

    /// <summary>Admins only. Maintenance operations.</summary>
    public const string CanAdminister = "CanAdminister";

    public static void Configure(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(CanView, p => p.RequireAuthenticatedUser());
        options.AddPolicy(CanOperate, p => p.RequireRole(Roles.Operator, Roles.Admin));
        options.AddPolicy(CanAdminister, p => p.RequireRole(Roles.Admin));
    }
}
