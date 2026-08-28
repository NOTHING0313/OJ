using System.Net;
using OnlineJudge.Application.Common;

namespace OnlineJudge.Infrastructure.Teams;

public class TeamRepositoryUrlValidator(TeamProjectOptions options)
{
    public Result<string> ValidateAndNormalize(string repositoryUrl)
    {
        if (!Uri.TryCreate(repositoryUrl?.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure("Only HTTPS Git repository URLs are allowed.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return Result<string>.Failure("Repository URL credentials are not allowed.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return Result<string>.Failure("Repository URL query and fragment are not allowed.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host)
            || IPAddress.TryParse(uri.Host, out _)
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Failure("Repository host is not allowed.");
        }

        var allowed = options.AllowedGitHosts.Any(host =>
            string.Equals(host?.Trim(), uri.Host, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            return Result<string>.Failure("Repository host is not allowed.");
        }

        if (string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/")
        {
            return Result<string>.Failure("Repository URL must include a repository path.");
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Host = uri.Host.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Query = string.Empty,
            Fragment = string.Empty,
            Path = uri.AbsolutePath.TrimEnd('/')
        };

        return Result<string>.Success(builder.Uri.AbsoluteUri.TrimEnd('/'));
    }
}
