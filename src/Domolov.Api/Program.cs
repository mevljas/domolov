using Domolov.Api.Hosting;
using Domolov.Application.Auth;
using Domolov.Application.Options;
using Domolov.Infrastructure;
using Domolov.Infrastructure.Configuration;
using Domolov.Infrastructure.Persistence;
using Domolov.Infrastructure.Scanning;

if (args is ["hash-password", var password, ..])
{
    Console.WriteLine(PasswordHashing.Hash(password));
    return 0;
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddDomolovEnvironmentVariables();
if (DomolovHosting.IsOpenApiGeneration)
{
    builder.Configuration.AddInMemoryCollection(DomolovHosting.OpenApiGenerationSettings);
}

builder.AddDomolovLogging();
builder.Services.AddDomolovInfrastructure(builder.Configuration);
builder.Services.AddDomolovApi(builder.Configuration, builder.Environment);

var app = builder.Build();

switch (args)
{
    case ["migrate", ..]:
        await app.MigrateDatabaseAsync();
        return 0;
    case ["browser-check", ..]:
        await using (var scope = app.Services.CreateAsyncScope())
        {
            Console.WriteLine(
                await scope
                    .ServiceProvider.GetRequiredService<BrowserCheck>()
                    .RunAsync(CancellationToken.None)
            );
        }

        return 0;
}

if (
    DependencyInjection.ReadRole(app.Configuration) == DomolovRole.All
    && !DomolovHosting.IsOpenApiGeneration
)
{
    // Single-process installs and local dev migrate on start; split deployments run `migrate`.
    await app.MigrateDatabaseAsync();
}

app.UseDomolovApi();
await app.RunAsync();
return 0;

/// <summary>Marker for WebApplicationFactory.</summary>
public partial class Program;
