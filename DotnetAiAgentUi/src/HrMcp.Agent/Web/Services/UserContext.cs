// src/HrMcp.Agent/Web/Services/UserContext.cs
// TODO (Task B5): Replace this stub with the full implementation.
namespace HrMcp.Agent.Web.Services;

/// <summary>
/// Provides the current authenticated user's context to Blazor components and services.
/// Full implementation is delivered in Task B5.
/// </summary>
public sealed class UserContext
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public bool IsAuthenticated => UserId is not null;
}
