namespace Domolov.Api.Auth;

/// <summary>
/// Requires <c>X-Requested-With: domolov</c> on unsafe methods. Browsers cannot send custom
/// headers cross-site without a CORS preflight (which Domolov never approves), so this blocks CSRF
/// on top of the SameSite=Strict session cookie.
/// </summary>
public sealed class CsrfHeaderFilter : IEndpointFilter
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "domolov";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var request = context.HttpContext.Request;
        if (
            HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || string.Equals(
                request.Headers[HeaderName],
                HeaderValue,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return await next(context);
        }

        return Results.Problem(
            title: "Missing CSRF header",
            detail: $"Send the header '{HeaderName}: {HeaderValue}' with state-changing requests.",
            statusCode: StatusCodes.Status403Forbidden
        );
    }
}
