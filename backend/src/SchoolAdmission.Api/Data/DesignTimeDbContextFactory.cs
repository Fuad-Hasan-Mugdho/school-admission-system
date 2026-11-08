using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolAdmission.Api.Data;

/// <summary>Used only by `dotnet ef` tooling to add/script migrations; never at runtime.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdmissionDbContext>
{
    public AdmissionDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                               ?? "Host=localhost;Port=5432;Database=admission;Username=admission;Password=admission";
        var options = new DbContextOptionsBuilder<AdmissionDbContext>()
            .UseNpgsql(ConnectionStrings.Normalize(connectionString))
            .Options;
        return new AdmissionDbContext(options);
    }
}
