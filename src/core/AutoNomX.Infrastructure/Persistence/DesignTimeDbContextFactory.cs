using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoNomX.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AutoNomXDbContext>
{
    public AutoNomXDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AutoNomXDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=autonomx;Username=autonomx;Password=autonomx_dev",
            b => b.MigrationsAssembly(typeof(AutoNomXDbContext).Assembly.FullName));

        return new AutoNomXDbContext(optionsBuilder.Options);
    }
}
