using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace PharmazapPlus.API.Extensions;

public class SwaggerSecurityFixMiddleware
{
    private readonly RequestDelegate _next;

    public SwaggerSecurityFixMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Value?.EndsWith("swagger.json") != true)
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await _next(context);

        buffer.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(buffer).ReadToEndAsync();
        json = Regex.Replace(json, @"""security"":\s*\[\s*\{\s*\}\s*\]", @"""security"":[{""Bearer"":[]}]");

        context.Response.Body = originalBody;
        context.Response.ContentLength = Encoding.UTF8.GetByteCount(json);
        await context.Response.WriteAsync(json);
    }
}

public static class SwaggerSecurityFixMiddlewareExtensions
{
    public static IApplicationBuilder UseSwaggerSecurityFix(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SwaggerSecurityFixMiddleware>();
    }
}