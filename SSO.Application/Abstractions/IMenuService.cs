using Shared;

namespace SSO.Application.Abstractions;

/// <summary>
/// Builds the navigation tree visible to a caller, given the permission codes the
/// caller holds. Group headers with no visible children are pruned.
/// </summary>
public interface IMenuService
{
    Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(
        IReadOnlySet<string> permissions, CancellationToken cancellationToken = default);
}
