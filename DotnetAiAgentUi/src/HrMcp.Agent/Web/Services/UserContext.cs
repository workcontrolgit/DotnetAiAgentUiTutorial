using Microsoft.AspNetCore.Components.Authorization;

namespace HrMcp.Agent.Web.Services;

public sealed class UserContext(AuthenticationStateProvider authStateProvider)
{
    public async Task<string?> GetUserIdAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true;
    }
}
