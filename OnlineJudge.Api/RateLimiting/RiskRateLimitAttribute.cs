namespace OnlineJudge.Api.RateLimiting;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RiskRateLimitAttribute(string policyName) : Attribute
{
    public string PolicyName { get; } = policyName;
}
