using Domolov.Application.Common;
using Domolov.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Domolov.Api.Hosting;

/// <summary>Maps domain and application exceptions to RFC 9457 problem responses.</summary>
public sealed class DomolovExceptionHandler(
    IProblemDetailsService problems,
    ILogger<DomolovExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        ProblemDetails? problem = exception switch
        {
            DomainRuleException rule => new HttpValidationProblemDetails(
                new Dictionary<string, string[]> { [rule.Field] = [rule.Message] }
            )
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more fields are invalid.",
                Detail = rule.Message,
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = $"{notFound.Resource} not found",
                Detail = notFound.Message,
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = conflict.Message,
            },
            PreconditionFailedException precondition => new ProblemDetails
            {
                Status = StatusCodes.Status412PreconditionFailed,
                Title = "Precondition failed",
                Detail = precondition.Message,
            },
            BadHttpRequestException bad => new ProblemDetails
            {
                Status = bad.StatusCode,
                Title = "Bad request",
                Detail = bad.Message,
            },
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path
            );
            return false;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status400BadRequest;
        return await problems.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception,
            }
        );
    }
}
