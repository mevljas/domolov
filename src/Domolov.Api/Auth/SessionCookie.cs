using System.Security.Claims;
using Domolov.Application.Auth;
using Domolov.Domain.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Domolov.Api.Auth;

/// <summary>Cookie authentication backed by revocable OperatorSessions.</summary>
public static class SessionCookie
{
    public const string CookieName = "domolov_session";
    public const string SessionIdClaim = "sid";

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx => WriteStatus(ctx.Response, StatusCodes.Status401Unauthorized),
            OnRedirectToAccessDenied = ctx =>
                WriteStatus(ctx.Response, StatusCodes.Status403Forbidden),
            OnValidatePrincipal = ValidateSessionAsync,
        };
    }

    public static ClaimsPrincipal CreatePrincipal(OperatorSession session) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, LoginHandler.OperatorName),
                    new Claim(SessionIdClaim, session.Id.ToString()),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme
            )
        );

    public static Guid? SessionId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionIdClaim), out var id) ? id : null;

    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext ctx)
    {
        var sessionId = ctx.Principal is null ? null : SessionId(ctx.Principal);
        var validator = ctx.HttpContext.RequestServices.GetRequiredService<SessionValidator>();
        if (
            sessionId is not Guid id
            || !await validator.ValidateAsync(id, ctx.HttpContext.RequestAborted)
        )
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    private static Task WriteStatus(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }
}
