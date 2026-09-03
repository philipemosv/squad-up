using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SquadUp.Identity.Infrastructure;

public static class BrowserSessionExtensions
{
    public const string AuthenticationScheme = "SquadUp.Session";
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";
    public const string SessionCookieName = "__Host-SquadUp.Session";
    public const string AntiforgeryCookieName = "__Host-SquadUp.Antiforgery";

    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    public static IServiceCollection AddBrowserSession(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("SquadUp.Api");
        var keysPath = configuration["BrowserSession:DataProtectionKeysPath"];
        if (!string.IsNullOrEmpty(keysPath))
        {
            if (!Path.IsPathFullyQualified(keysPath))
            {
                throw new InvalidOperationException(
                    "BrowserSession:DataProtectionKeysPath must be an absolute path when configured.");
            }

            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationScheme;
                options.DefaultChallengeScheme = AuthenticationScheme;
                options.DefaultSignInScheme = AuthenticationScheme;
                options.DefaultSignOutScheme = AuthenticationScheme;
            })
            .AddCookie(AuthenticationScheme, options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = SessionLifetime;
                options.SlidingExpiration = false;
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = static context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = static context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        return services;
    }
}
