using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudge.Api.Controllers;

namespace OnlineJudge.Tests.Authorization;

public class ProblemAccessTests
{
    [Theory]
    [InlineData(typeof(ProblemsController), nameof(ProblemsController.GetProblems))]
    [InlineData(typeof(ProblemsController), nameof(ProblemsController.GetProblem))]
    [InlineData(typeof(ChallengesController), nameof(ChallengesController.GetChallenge))]
    [InlineData(typeof(SubmissionsController), nameof(SubmissionsController.CreateSubmission))]
    [InlineData(typeof(SubmissionsController), nameof(SubmissionsController.CreateChoiceSubmission))]
    public async Task ReadAndAnswerEndpoints_RejectAnonymousAndAcceptAuthenticatedUsers(Type controller, string action)
    {
        var metadata = controller.GetCustomAttributes().Concat(controller.GetMethod(action)!.GetCustomAttributes()).ToArray();
        Assert.Empty(metadata.OfType<IAllowAnonymous>());
        var authorization = metadata.OfType<IAuthorizeData>().ToArray();
        Assert.NotEmpty(authorization);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        await using var provider = services.BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), authorization);
        Assert.NotNull(policy);
        var evaluator = provider.GetRequiredService<IAuthorizationService>();
        Assert.False((await evaluator.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded);
        var signedIn = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test"));
        Assert.True((await evaluator.AuthorizeAsync(signedIn, null, policy)).Succeeded);
    }

    [Fact]
    public void AuthoringPolicyAndPublicChallengeOverview_ArePreserved()
    {
        var authoring = typeof(ProblemsController).GetMethod(nameof(ProblemsController.GetProblemAuthoring))!;
        Assert.Contains(authoring.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Policy == "RequireProblemSetter");
        Assert.NotNull(typeof(ChallengesController).GetMethod(nameof(ChallengesController.GetChallenges))!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(typeof(ChallengesController).GetMethod(nameof(ChallengesController.GetLeaderboard))!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void ProblemAndChallengeQuestionRoutes_RequireLoginBeforeMounting()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "frontend", "src", "main.tsx"));
        foreach (var page in new[] { "ProblemListPage", "ProblemDetailPage", "ChallengeDetailPage", "ChallengeTaskDetailPage", "ChallengeTaskAnswerPage" })
            Assert.Contains($"element={{<ProtectedRoute><{page} /></ProtectedRoute>}}", source);
    }
}
