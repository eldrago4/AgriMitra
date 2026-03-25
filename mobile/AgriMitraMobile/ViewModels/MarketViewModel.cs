using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

public class MspRow
{
    public string Crop    { get; set; } = string.Empty;
    public string Variety { get; set; } = string.Empty;
    public string Price   { get; set; } = string.Empty;
    public string Unit    { get; set; } = string.Empty;
    public double BarWidth { get; set; }
}

public class MandiInfo
{
    public string Name       { get; set; } = string.Empty;
    public string District   { get; set; } = string.Empty;
    public string Distance   { get; set; } = string.Empty;
    public string Phone      { get; set; } = string.Empty;
    public bool   IsOpen     { get; set; }
    public string StatusText => IsOpen ? "Open Now" : "Closed";
}

public partial class MarketViewModel : BaseViewModel
{
    private readonly IApiService _api;

    [ObservableProperty] private string   _lastUpdated   = "Loading...";
    [ObservableProperty] private MandiInfo? _nearestMandi;
    [ObservableProperty] private bool     _hasLocation;
    [ObservableProperty] private bool     _mandiLoading;

    public ObservableCollection<MspRow> MspRows { get; } = new();

    public MarketViewModel(IApiService api)
    {
        _api  = api;
        Title = "Market Prices";
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var msp = await _api.GetMspDataAsync();
            MspRows.Clear();

            var rows = (msp?.Data ?? DefaultMsp())
                           .Where(r => r.EffectivePrice > 0).ToList();
            double maxPrice = rows.Max(r => (double)r.EffectivePrice);
            foreach (var r in rows)
            {
                MspRows.Add(new MspRow
                {
                    Crop     = r.Crop,
                    Variety  = r.Variety ?? r.Group ?? "",
                    Price    = $"₹{r.EffectivePrice:N0}",
                    Unit     = r.Unit ?? "/qtl",
                    BarWidth = r.EffectivePrice / maxPrice * 220,
                });
            }
            LastUpdated = msp != null
                ? $"MSP {msp.Year} (Kharif/Rabi)"
                : "MSP 2023-24 (offline data)";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FindNearestMandiAsync()
    {
        MandiLoading = true;
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                        ?? await Geolocation.Default.GetLocationAsync(
                               new GeolocationRequest(GeolocationAccuracy.Medium,
                                                      TimeSpan.FromSeconds(10)));
            if (location == null) return;

            HasLocation = true;
            var result  = await _api.GetNearestMandiAsync(location.Latitude, location.Longitude);
            if (result?.Nearest != null)
            {
                NearestMandi = new MandiInfo
                {
                    Name     = result.Nearest.Name,
                    District = result.Nearest.District,
                    Distance = $"{result.Nearest.DistanceKm:F1} km away",
                    Phone    = result.Nearest.Phone,
                    IsOpen   = result.Nearest.IsOpen,
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Geolocation error: {ex.Message}");
        }
        finally { MandiLoading = false; }
    }

    private static List<MspEntry> DefaultMsp() => new()
    {
        new("Rice",      "Common",    2183, "/qtl"),
        new("Wheat",     "All",       2275, "/qtl"),
        new("Soybean",   "Yellow",    4600, "/qtl"),
        new("Sugarcane", "FRP",        315, "/qtl"),
        new("Maize",     "All",       2090, "/qtl"),
        new("Cotton",    "Medium",    6620, "/qtl"),
    };
}
