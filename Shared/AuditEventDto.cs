namespace Shared;

/// <summary>One row in the activity-monitoring viewer (read model).</summary>
public class AuditEventDto
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? ActorUserName { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>Generic paged result envelope for list endpoints.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
