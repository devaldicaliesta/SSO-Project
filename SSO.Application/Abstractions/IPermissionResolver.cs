namespace SSO.Application.Abstractions;

/// <summary>
/// Computes a user's effective permission set:
/// <code>(roles' permissions ∪ user Grant overrides) \ user Deny overrides</code>
/// A Deny override always wins (least privilege). The result is the set of
/// permission codes the user holds, used to issue <c>permission</c> token claims
/// and to filter the navigation menu.
/// </summary>
public interface IPermissionResolver
{
    Task<IReadOnlySet<string>> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
}
