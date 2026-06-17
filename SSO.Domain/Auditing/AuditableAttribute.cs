namespace SSO.Domain.Auditing;

/// <summary>
/// Marks an entity whose create/update/delete operations should be captured by
/// the EF Core SaveChanges audit interceptor. Apply to any entity whose changes
/// must appear in the activity-monitoring trail.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AuditableAttribute : Attribute
{
}
