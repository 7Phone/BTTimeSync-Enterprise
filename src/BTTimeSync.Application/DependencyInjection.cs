using BTTimeSync.Application.Configuration;
using BTTimeSync.Application.System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BTTimeSync.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ISystemClock, WindowsSystemClock>();

        services.AddSingleton<AppConfig>(_ =>
        {
            return configuration
                .GetSection("BTTimeSync")
                .Get<AppConfig>()
                ?? new AppConfig();
        });

        services.AddSingleton<TimeSyncApplication>();

        return services;
    }
}