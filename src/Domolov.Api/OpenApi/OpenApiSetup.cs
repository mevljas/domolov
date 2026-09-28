using Domolov.Api.Auth;
using Microsoft.OpenApi;

namespace Domolov.Api.OpenApi;

/// <summary>OpenAPI 3.1 document metadata and the cookie security scheme.</summary>
public static class OpenApiSetup
{
    public const string SecuritySchemeId = "sessionCookie";

    public static IServiceCollection AddDomolovOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(
            "v1",
            o =>
            {
                o.AddDocumentTransformer(
                    (document, _, _) =>
                    {
                        document.Info = new OpenApiInfo
                        {
                            Title = "Domolov API",
                            Version = "v1",
                            Description =
                                "REST API of Domolov, a self-hosted real-estate listing hunter. "
                                + "Sign in with POST /api/session; the session cookie authenticates later calls. "
                                + "State-changing requests must send the header `X-Requested-With: domolov`. "
                                + "Errors use RFC 9457 problem details.",
                            License = new OpenApiLicense { Name = "MIT" },
                        };
                        document.Components ??= new OpenApiComponents();
                        document.Components.SecuritySchemes ??=
                            new Dictionary<string, IOpenApiSecurityScheme>();
                        document.Components.SecuritySchemes[SecuritySchemeId] =
                            new OpenApiSecurityScheme
                            {
                                Type = SecuritySchemeType.ApiKey,
                                In = ParameterLocation.Cookie,
                                Name = SessionCookie.CookieName,
                                Description = "Session cookie set by POST /api/session.",
                            };
                        return Task.CompletedTask;
                    }
                );
                o.AddOperationTransformer(
                    (operation, context, _) =>
                    {
                        var anonymous = context
                            .Description.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()
                            .Any();
                        if (!anonymous)
                        {
                            operation.Security ??= [];
                            operation.Security.Add(
                                new OpenApiSecurityRequirement
                                {
                                    [new OpenApiSecuritySchemeReference(SecuritySchemeId)] = [],
                                }
                            );
                        }

                        return Task.CompletedTask;
                    }
                );
            }
        );
}
