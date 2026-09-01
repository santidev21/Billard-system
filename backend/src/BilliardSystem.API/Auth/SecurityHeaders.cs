namespace BilliardSystem.API.Auth;

public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(self), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
            context.Response.Headers.Append("X-XSS-Protection", "0");
            await next();
        });
    }
}
