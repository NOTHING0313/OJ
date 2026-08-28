using System.Net;
using System.Net.Sockets;
using OnlineJudge.Application.Common;

namespace OnlineJudge.Infrastructure.Teams;

public interface ITeamGitHostResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class TeamGitHostResolver : ITeamGitHostResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        return Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}

public sealed class TeamGitRemoteSecurityValidator(
    TeamRepositoryUrlValidator staticValidator,
    ITeamGitHostResolver hostResolver,
    TeamProjectOptions options)
{
    public async Task<Result<string>> ValidateAsync(string repositoryUrl, CancellationToken cancellationToken)
    {
        var staticResult = staticValidator.ValidateAndNormalize(repositoryUrl);
        if (staticResult.IsFailure || staticResult.Value is null)
        {
            return Result<string>.Failure(staticResult.ErrorMessage ?? "Repository URL is not allowed.");
        }

        var uri = new Uri(staticResult.Value, UriKind.Absolute);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.GitTimeoutSeconds, 1, 300)));
            var addresses = await hostResolver.ResolveAsync(uri.Host, timeout.Token);
            if (addresses.Length == 0 || addresses.Any(address => !IsPublicRoutable(address)))
            {
                return Result<string>.Failure("Repository host did not resolve exclusively to public addresses.");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure("Repository host could not be resolved.");
        }
        catch (SocketException)
        {
            return Result<string>.Failure("Repository host could not be resolved.");
        }
        catch (ArgumentException)
        {
            return Result<string>.Failure("Repository host could not be resolved.");
        }

        return Result<string>.Success(staticResult.Value);
    }

    public static bool IsPublicRoutable(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var first = bytes[0];
            var second = bytes[1];
            return first != 0
                && first != 10
                && first != 127
                && !(first == 100 && second is >= 64 and <= 127)
                && !(first == 169 && second == 254)
                && !(first == 172 && second is >= 16 and <= 31)
                && !(first == 192 && second == 0)
                && !(first == 192 && second == 168)
                && !(first == 198 && second is 18 or 19)
                && !(first == 192 && second == 0 && bytes[2] == 2)
                && !(first == 198 && second == 51 && bytes[2] == 100)
                && !(first == 203 && second == 0 && bytes[2] == 113)
                && first < 224;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6) return false;
        if (address.Equals(IPAddress.IPv6None)
            || address.Equals(IPAddress.IPv6Loopback)
            || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast)
        {
            return false;
        }

        if ((bytes[0] & 0xfe) == 0xfc) return false;
        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0) return false;
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) return false;
        return true;
    }
}
