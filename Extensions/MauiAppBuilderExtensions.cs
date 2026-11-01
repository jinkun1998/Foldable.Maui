using Foldable.Maui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace Foldable.Maui.Extensions;

public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Configures Foldable.Maui services and starts listening to native foldable lifecycle events.
    /// </summary>
    public static MauiAppBuilder UseFoldable(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IFoldableService>(_ => FoldableService.Current);

        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android
                .OnResume(activity =>
                {
                    FoldableService.Current.StartListening();
                })
                .OnPause(activity =>
                {
                    FoldableService.Current.StopListening();
                }));
#endif
        });

        return builder;
    }
}
