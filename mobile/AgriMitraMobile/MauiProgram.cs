using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using AgriMitraMobile.Services;
using AgriMitraMobile.ViewModels;
using AgriMitraMobile.Views;

namespace AgriMitraMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                fonts.AddFont("OpenSans-Regular.ttf",      "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf",     "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── Services ────────────────────────────────────────────────────
        builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<IOnDeviceInferenceService, OnDeviceInferenceService>();
        builder.Services.AddSingleton<IApiService, ApiService>();

        // ── ViewModels ──────────────────────────────────────────────────
        builder.Services.AddTransient<RegistrationViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<FarmMapViewModel>();
        builder.Services.AddTransient<CropDetailsViewModel>();
        builder.Services.AddTransient<PredictionLoadingViewModel>();
        builder.Services.AddTransient<PredictionResultViewModel>();
        builder.Services.AddTransient<MyFieldsViewModel>();
        builder.Services.AddTransient<MarketViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ── Pages ───────────────────────────────────────────────────────
        builder.Services.AddTransient<RegistrationPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<FarmMapPage>();
        builder.Services.AddTransient<CropDetailsPage>();
        builder.Services.AddTransient<PredictionLoadingPage>();
        builder.Services.AddTransient<PredictionResultPage>();
        builder.Services.AddTransient<MyFieldsPage>();
        builder.Services.AddTransient<MarketPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
