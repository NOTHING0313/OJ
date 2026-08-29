using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OnlineJudge.Infrastructure.Persistence;

public static class ScoringIdentityTransactionLock
{
    public static async Task AcquireAsync(OnlineJudgeDbContext dbContext, string scope, IReadOnlyList<Guid> identityParts, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational()) return;
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A scoring identity lock requires an active database transaction.");
        }

        var identity = $"{scope}:{string.Join(':', identityParts.Select(part => part.ToString("N")))}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey});", cancellationToken);
    }
}
