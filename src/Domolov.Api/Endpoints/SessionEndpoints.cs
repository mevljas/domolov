using System.Security.Claims;
using Domolov.Api.Auth;
using Domolov.Api.Hosting;
using Domolov.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Domolov.Api.Endpoints;

/// <summary>Sign-in, sign-out and session management.</summary>
public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this RouteGroupBuilder api)
    {
        var session = api.MapGroup("/session").WithTags("Session");

        session
            .MapGet(
                "",
                async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>> (
                    ClaimsPrincipal user,
                    GetSessionHandler handler,
                    CancellationToken ct
                ) =>
                    SessionCookie.SessionId(user) is Guid id
                        ? TypedResults.Ok(await handler.HandleAsync(id, ct))
                        : TypedResults.Unauthorized()
            )
            .WithName("GetSession")
            .WithSummary("Get the current session (401 when signed out)");

        session
            .MapPost(
                "",
                async Task<Results<NoContent, ProblemHttpResult>> (
                    LoginRequest request,
                    HttpContext http,
                    LoginHandler handler,
                    CancellationToken ct
                ) =>
                {
                    var created = await handler.HandleAsync(
                        request,
                        http.Request.Headers.UserAgent.ToString(),
                        http.Connection.RemoteIpAddress?.ToString(),
                        ct
                    );
                    if (created is null)
                    {
                        return TypedResults.Problem(
                            title: "Wrong password",
                            detail: "That password did not match. Try again.",
                            statusCode: StatusCodes.Status401Unauthorized
                        );
                    }

                    await http.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        SessionCookie.CreatePrincipal(created),
                        new AuthenticationProperties
                        {
                            IsPersistent = true,
                            ExpiresUtc = created.ExpiresAt,
                            AllowRefresh = true,
                        }
                    );
                    return TypedResults.NoContent();
                }
            )
            .AllowAnonymous()
            .RequireRateLimiting(DomolovHosting.LoginRateLimitPolicy)
            .WithName("SignIn")
            .WithSummary("Sign in with the operator password; sets the session cookie")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        session
            .MapDelete(
                "",
                async Task<NoContent> (
                    ClaimsPrincipal user,
                    HttpContext http,
                    RevokeSessionsHandler handler,
                    CancellationToken ct
                ) =>
                {
                    if (SessionCookie.SessionId(user) is Guid id)
                    {
                        await handler.HandleAsync(id, ct);
                    }

                    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return TypedResults.NoContent();
                }
            )
            .WithName("SignOut")
            .WithSummary("Sign out of this browser");

        var sessions = api.MapGroup("/sessions").WithTags("Session");

        sessions
            .MapGet(
                "",
                async Task<Ok<IReadOnlyList<SessionListItem>>> (
                    ClaimsPrincipal user,
                    ListSessionsHandler handler,
                    CancellationToken ct
                ) =>
                    TypedResults.Ok(
                        await handler.HandleAsync(SessionCookie.SessionId(user) ?? Guid.Empty, ct)
                    )
            )
            .WithName("ListSessions")
            .WithSummary("List active sessions");

        sessions
            .MapDelete(
                "",
                async Task<NoContent> (
                    HttpContext http,
                    RevokeSessionsHandler handler,
                    CancellationToken ct
                ) =>
                {
                    await handler.RevokeAllAsync(ct);
                    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return TypedResults.NoContent();
                }
            )
            .WithName("SignOutEverywhere")
            .WithSummary("Revoke every session, including this one");

        sessions
            .MapDelete(
                "/{id:guid}",
                async Task<NoContent> (
                    Guid id,
                    ClaimsPrincipal user,
                    HttpContext http,
                    RevokeSessionsHandler handler,
                    CancellationToken ct
                ) =>
                {
                    await handler.HandleAsync(id, ct);
                    if (SessionCookie.SessionId(user) == id)
                    {
                        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }

                    return TypedResults.NoContent();
                }
            )
            .WithName("RevokeSession")
            .WithSummary("Revoke one session");
    }
}
