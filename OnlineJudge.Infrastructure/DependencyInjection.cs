using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudge.Application.Account.Services;
using OnlineJudge.Application.Challenges.Services;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Application.Profile.Services;
using OnlineJudge.Application.Problems.Services;
using OnlineJudge.Application.SiteSettings.Services;
using OnlineJudge.Application.Submissions.Services;
using OnlineJudge.Application.Auth.Services;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Application.Sms.Services;
using OnlineJudge.Application.Users.Services;
using OnlineJudge.Infrastructure.Account;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Judging;
using OnlineJudge.Infrastructure.Judging.Function;
using OnlineJudge.Infrastructure.Judging.Runners;
using OnlineJudge.Infrastructure.Judging.Sandbox;
using OnlineJudge.Infrastructure.Leaderboards;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Profile;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Infrastructure.SiteSettings;
using OnlineJudge.Infrastructure.Sms;
using OnlineJudge.Infrastructure.Submissions;
using OnlineJudge.Infrastructure.Users;
using StackExchange.Redis;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.Email;
using OnlineJudge.Infrastructure.ContentVisibility;

namespace OnlineJudge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<OnlineJudgeDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ContentVisibilityPolicy>();

        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<IProblemService, ProblemService>();
        services.AddScoped<IProblemJudgeAssetService, ProblemJudgeAssetService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISiteSettingsService, SiteSettingsService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISmsVerificationService, SmsVerificationService>();
        services.AddScoped<ISmsSender, DevSmsSender>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IEmailSender>(provider =>
        {
            var providerName = configuration["Email:Provider"] ?? "Dev";
            return string.Equals(providerName, "Smtp", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider)
                : ActivatorUtilities.CreateInstance<DevEmailSender>(provider);
        });
        services.AddScoped<PasswordHasher>();
        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IJudgeQueue, RedisJudgeQueue>();
        services.AddSingleton<IProblemJudgeAssetStorage, ProblemJudgeAssetStorage>();
        services.AddScoped<IJudgeCompileAssetLoader, JudgeCompileAssetLoader>();
        services.AddScoped<IJudgeSandbox, DockerJudgeSandbox>();
        services.AddScoped<IFunctionJudgeCodeBuilder, Cpp17FunctionJudgeCodeBuilder>();
        services.AddScoped<C11FunctionJudgeCodeBuilder>();
        services.AddScoped<CSharpFunctionJudgeCodeBuilder>();
        services.AddScoped<IJudgeRunnerFactory, JudgeRunnerFactory>();
        services.AddScoped<IJudgeRunner, Cpp17JudgeRunner>();
        services.AddScoped<IJudgeRunner, C11JudgeRunner>();
        services.AddScoped<IJudgeRunner, CSharpJudgeRunner>();

        return services;
    }
}
