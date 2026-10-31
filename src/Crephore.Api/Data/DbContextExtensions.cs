using System.Text.RegularExpressions;
using Crephore.Api.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Crephore.Api.Data;

public static partial class DbContextExtensions
{
    // MySQL 8 prefixes the table name: "Duplicate entry 'x' for key 'events.IX_events_Subdomain'"
    [GeneratedRegex(@"for key '(?:[^'.]+\.)?(?<index>[^']+)'")]
    private static partial Regex DuplicateKeyRegex();

    /// <summary>
    /// Saves changes, turning a violation of <paramref name="indexName"/> into a 400 error on <paramref name="field"/>.
    /// Services still check uniqueness before saving (so the error comes with any other field errors);
    /// this covers two requests that pass that check at the same time, where the index rejects the second.
    /// </summary>
    public static async Task<int> SaveChangesOrThrowIfDuplicateAsync(
        this DbContext context,
        string indexName,
        string field,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(indexName))
        {
            throw new AppValidationException(message, new Dictionary<string, List<string>>
            {
                [field] = [message],
            });
        }
    }

    /// <summary>Whether the save failed because it would duplicate a value in the unique index <paramref name="indexName"/>.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception, string indexName)
    {
        if (exception.InnerException is not MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry } mySqlException)
            return false;

        var match = DuplicateKeyRegex().Match(mySqlException.Message);
        return match.Success && match.Groups["index"].Value == indexName;
    }
}
