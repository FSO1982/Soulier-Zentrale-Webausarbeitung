using Microsoft.AspNetCore.Authorization;
using Soulier.Zentrale.Application;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Api;

public sealed class ActiveHumanEnrollmentRequirement : IAuthorizationRequirement;

public sealed class ActiveHumanEnrollmentHandler(IHumanPrincipalRegistry registry)
    : AuthorizationHandler<ActiveHumanEnrollmentRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveHumanEnrollmentRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
            return;

        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        var principal = await registry.FindByOidcSubjectAsync(subject, cancellationToken);
        if (principal is { Status: HumanPrincipalStatus.Active })
            context.Succeed(requirement);
    }
}

/// <summary>
/// Fail-closed fallback. A production composition root must replace this with a persistent registry.
/// </summary>
public sealed class DenyAllHumanPrincipalRegistry : IHumanPrincipalRegistry
{
    public Task<HumanPrincipal?> FindByOidcSubjectAsync(
        string oidcSubject,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<HumanPrincipal?>(null);
}
