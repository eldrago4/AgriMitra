namespace AgriMitraMobile.Services;

public interface IConnectivityService
{
    bool IsConnected { get; }
}

public class ConnectivityService : IConnectivityService
{
    public bool IsConnected =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
}
