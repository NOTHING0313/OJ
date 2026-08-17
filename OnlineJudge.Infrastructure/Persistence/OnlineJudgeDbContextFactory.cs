using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OnlineJudge.Infrastructure.Persistence;

public class OnlineJudgeDbContextFactory : IDesignTimeDbContextFactory<OnlineJudgeDbContext>
{
    public OnlineJudgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=online_judge;Username=oj_user;Password=oj_password";

        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new OnlineJudgeDbContext(options);
    }
}
