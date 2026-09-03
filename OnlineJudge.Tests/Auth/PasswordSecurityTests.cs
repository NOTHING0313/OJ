using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Auth.Requests;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Auth;

public sealed class PasswordSecurityTests
{
    [Fact]
    public void Policy_UsesUnicodeCodePointsAndAllowsSpacesAndUnicode()
    {
        var policy = new PasswordPolicy();

        var result = policy.Validate("这是 一个 足够 长而且 独特 的安全密码 2026");

        Assert.Null(result);
    }

    [Fact]
    public void Policy_EnforcesLengthBoundsByUnicodeCodePoint()
    {
        var policy = new PasswordPolicy();

        Assert.Contains("at least 8", policy.Validate(string.Concat(Enumerable.Repeat("🙂", 7))));
        Assert.Null(policy.Validate(string.Concat(Enumerable.Repeat("🙂", 8))));
        Assert.Contains("128", policy.Validate(string.Concat(Enumerable.Repeat("🙂", 129))));
    }

    [Fact]
    public void Policy_RejectsOversizedUtf16InputBeforeNormalization()
    {
        var oversized = new string('a', PasswordPolicy.MaximumLength * 2 + 1);

        var result = new PasswordPolicy().Validate(oversized);

        Assert.Contains("128", result);
    }

    [Theory]
    [InlineData("correct horse battery staple")]
    [InlineData("unrealstudio2026")]
    [InlineData("answerer-2026-xyz")]
    [InlineData("answerer20262026")]
    public void Policy_RejectsCommonAndContextSpecificVariants(string password)
    {
        var result = new PasswordPolicy().Validate(password, "answerer", "answerer@example.test");

        Assert.Equal("Password is too common or too closely related to account information.", result);
    }

    [Fact]
    public void Hasher_EmitsV2AndNormalizesUnicode()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("Cafe\u0301 has a long unique passphrase");

        Assert.StartsWith("v2.600000.", hash);
        Assert.True(hasher.VerifyPassword("Café has a long unique passphrase", hash));
        Assert.False(hasher.NeedsRehash(hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v2.600000.not-base64.not-base64")]
    [InlineData("v2.999999999.AA==.AA==")]
    public void Hasher_MalformedPersistedValueFailsClosed(string persistedHash)
    {
        Assert.False(new PasswordHasher().VerifyPassword("candidate", persistedHash));
    }

    [Fact]
    public async Task SuccessfulLegacyLogin_RehashesToV2WithoutChangingCredentials()
    {
        await using var dbContext = CreateDbContext();
        const string password = "legacy password accepted";
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "legacy-user",
            Email = "legacy@example.test",
            PasswordHash = CreateLegacyHash(password),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var hasher = new PasswordHasher();
        var service = new AuthService(
            dbContext,
            hasher,
            new JwtTokenGenerator(CreateConfiguration()),
            new FakeEmailVerificationService());
        var result = await service.LoginAsync(new LoginRequest { Account = user.UserName, Password = password });

        Assert.True(result.IsSuccess);
        Assert.StartsWith("v2.600000.", user.PasswordHash);
        Assert.True(hasher.VerifyPassword(password, user.PasswordHash));
    }

    private static string CreateLegacyHash(string password)
    {
        const int iterations = 100_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "OnlineJudge.Tests",
            ["Jwt:Audience"] = "OnlineJudge.Tests",
            ["Jwt:Secret"] = new string('x', 64),
            ["Jwt:ExpireMinutes"] = "120"
        }).Build();

    private sealed class FakeEmailVerificationService : IEmailVerificationService
    {
        public Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> VerifyCodeAsync(string scene, string email, string code, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
