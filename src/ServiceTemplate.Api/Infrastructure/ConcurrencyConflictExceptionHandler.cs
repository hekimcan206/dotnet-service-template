using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ServiceTemplate.Api.Infrastructure;

/// <summary>
/// Turns a lost write race into a 409 the client can retry, instead of a 500. Two shapes of race exist:
/// an optimistic-concurrency failure (row version changed under us) and two requests inserting the same
/// key at once (unique violation). Everything else falls through to the default ProblemDetails handler.
/// </summary>
internal sealed partial class ConcurrencyConflictExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ConcurrencyConflictExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsWriteConflict(exception))
        {
            return false;
        }

        LogConflict(logger, httpContext.Request.Path);
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The resource was modified by another request.",
                Detail = "Reload the resource and retry the operation.",
                Extensions = { ["code"] = "concurrency_conflict" },
            },
        });
    }

    internal static bool IsWriteConflict(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => true,
        _ => false,
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Write conflict on {Path}")]
    private static partial void LogConflict(ILogger logger, PathString path);
}
