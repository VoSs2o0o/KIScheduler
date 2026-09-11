using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KIScheduler.Infrastructure.Persistence;

public sealed class KischedulerDbContextFactory : IDesignTimeDbContextFactory<KischedulerDbContext>
{
    public KischedulerDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<KischedulerDbContext>();
        builder.UseSqlite("Data Source=kischedule.design.db;Foreign Keys=True;Default Timeout=5");
        return new KischedulerDbContext(builder.Options);
    }
}
