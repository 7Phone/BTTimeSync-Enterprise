using BTTimeSync.Core.Interfaces;
using BTTimeSync.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTTimeSync.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(
        this IServiceCollection services)
    {
        services.AddSingleton<
            ITimeSyncService,
            TimeSyncService>();

        services.AddSingleton<
            ITimeSyncSampler,
            TimeSyncSampler>();

        services.AddSingleton<
            IPacketTransport,
            PacketTransport>();

        services.AddSingleton<
            IBtspSession,
            BtspSession>();

        return services;
    }
}