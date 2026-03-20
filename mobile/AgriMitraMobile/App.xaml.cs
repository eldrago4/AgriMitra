using AgriMitraMobile.Services;

namespace AgriMitraMobile;

public partial class App : Application
{
    public App(ILocalDatabaseService db)
    {
        InitializeComponent();
        // Initialize local DB
        _ = db.InitAsync();
        MainPage = new AppShell();
    }
}
