using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SSO.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling (<c>dotnet ef migrations add</c> /
/// <c>database update</c>) can construct the context without booting the web host.
/// The connection string is read from the <c>SSO_CONNECTION</c> environment
/// variable, falling back to a local SQL Server. At runtime the host registers the
/// context via DI (with the audit interceptor) instead of using this factory.
/// </summary>
public sealed class SsoDbContextFactory : IDesignTimeDbContextFactory<SsoDbContext>
{
    public SsoDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SSO_CONNECTION")
            ?? "Server=DEVALDICALIESTA;Database=SSO-PROJECT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<SsoDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(SsoDbContextFactory).Assembly.FullName))
            .Options;

        return new SsoDbContext(options);
    }
}
