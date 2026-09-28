using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Domolov.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> create the context without starting the host.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DomolovDbContext>
{
    public DomolovDbContext CreateDbContext(string[] args)
    {
        var cs =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=domolov;Username=domolov;Password=domolov";
        var options = new DbContextOptionsBuilder<DomolovDbContext>().UseNpgsql(cs).Options;
        return new DomolovDbContext(options);
    }
}
