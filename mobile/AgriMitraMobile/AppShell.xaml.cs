using AgriMitraMobile.Views;

namespace AgriMitraMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for modal navigation
        Routing.RegisterRoute("farmmap",           typeof(FarmMapPage));
        Routing.RegisterRoute("cropdetails",       typeof(CropDetailsPage));
        Routing.RegisterRoute("predictionloading", typeof(PredictionLoadingPage));
        Routing.RegisterRoute("predictionresult",  typeof(PredictionResultPage));
    }
}
