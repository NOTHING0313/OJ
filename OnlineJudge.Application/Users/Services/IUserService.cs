using OnlineJudge.Application.Auth.Dtos;
using OnlineJudge.Application.Common;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Users.Services;

public interface IUserService
{
    Task<Result<PagedResult<AuthUserDto>>> GetUsersAsync(string? keyword, UserRole? role, bool? isBlacklisted, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AuthUserDto>>> GetProblemSettersAsync(CancellationToken cancellationToken = default);

    Task<Result<AuthUserDto>> PromoteToProblemSetterAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<AuthUserDto>> DemoteToAnswererAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result> BlacklistAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result> UnblacklistAsync(Guid userId, CancellationToken cancellationToken = default);
}
