using Microsoft.AspNetCore.Http.Extensions;

namespace Domolov.Api.Hosting;

/// <summary>
/// JSON enums are camelCase ("priceAsc"), but minimal APIs parse query-string enums
/// case-sensitively against the C# names ("PriceAsc"). Upper-cases the first letter of the
/// enum-valued query parameters so clients can use the same spelling everywhere.
/// </summary>
public sealed class EnumQueryCasing(RequestDelegate next)
{
    private static readonly HashSet<string> EnumParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "sort",
        "status",
        "dismissed",
        "stage",
        "state",
    };

    public Task InvokeAsync(HttpContext context)
    {
        var query = context.Request.Query;
        if (query.Count > 0 && query.Keys.Any(EnumParameters.Contains))
        {
            var builder = new QueryBuilder();
            foreach (var (key, values) in query)
            {
                foreach (var value in values)
                {
                    builder.Add(
                        key,
                        EnumParameters.Contains(key) && !string.IsNullOrEmpty(value)
                            ? char.ToUpperInvariant(value[0]) + value[1..]
                            : value ?? ""
                    );
                }
            }

            context.Request.QueryString = builder.ToQueryString();
        }

        return next(context);
    }
}
