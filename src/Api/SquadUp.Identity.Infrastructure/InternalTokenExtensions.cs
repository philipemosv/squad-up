using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadUp.Identity.Application;

namespace SquadUp.Identity.Infrastructure;

public static class InternalTokenExtensions
{
    public static IServiceCollection AddInternalTokenIssuer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<InternalTokenOptions>()
            .Bind(configuration.GetSection(InternalTokenOptions.SectionName))
            .Validate(InternalTokenOptions.IsValid, InternalTokenOptions.ValidationError)
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInternalAccessTokenIssuer, InternalAccessTokenIssuer>();

        return services;
    }
}
