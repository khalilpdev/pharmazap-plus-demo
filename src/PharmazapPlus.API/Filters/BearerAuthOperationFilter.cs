using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PharmazapPlus.API.Filters;

/// <summary>
/// Adds a JWT Bearer security requirement to all Swagger operations that are not marked
/// with <see cref="AllowAnonymousAttribute"/>, making the "Authorize" padlock appear in the
/// Swagger UI for every protected endpoint.
/// </summary>
internal sealed class BearerAuthOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Skip endpoints that explicitly allow anonymous access
        var hasAllowAnonymous =
            context.MethodInfo
                   .GetCustomAttributes(inherit: true)
                   .OfType<AllowAnonymousAttribute>()
                   .Any()
            || (context.MethodInfo.DeclaringType?
                       .GetCustomAttributes(inherit: true)
                       .OfType<AllowAnonymousAttribute>()
                       .Any() ?? false);

        if (hasAllowAnonymous)
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme),
                new List<string>()
            }
        });
    }
}
