using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

[QueryProperty(nameof(FieldId),      "fieldId")]
[QueryProperty(nameof(AreaHectares), "area")]
public partial class CropDetailsViewModel : BaseViewModel
{
    private readonly IApiService               _api;
    private readonly IOnDeviceInferenceService _onDevice;
    private readonly ILocalDatabaseService     _db;
    private readonly IConnectivityService      _conn;

    // Query parameters
    [ObservableProperty] private int    _fieldId;
    [ObservableProperty] private double _areaHectares;

    // Form fields
    [ObservableProperty] private string   _selectedCrop    = "Paddy(Deshaj)";
    [ObservableProperty] private DateTime _plantingDate    = DateTime.Today;
    [ObservableProperty] private bool     _useIotData;
    [ObservableProperty] private string   _season          = "Kharif";
    [ObservableProperty] private string   _dateLabel       = "Sowing / Planting Date";
    [ObservableProperty] private string   _irrigationType  = "Rainfed";

    // IoT input fields
    [ObservableProperty] private string _soilN       = "100";
    [ObservableProperty] private string _soilP       = "50";
    [ObservableProperty] private string _soilK       = "200";
    [ObservableProperty] private string _moisture    = "35";
    [ObservableProperty] private string _pH          = "6.5";
    [ObservableProperty] private string _temperature = "28";

    // Crops cultivated in Sindhudurg — using AGMARKNET commodity names
    public List<string> Crops { get; } = new()
    {
        "Paddy(Deshaj)",   // local rice varieties; main kharif crop
        "Coconut",
        "Cashewnuts",      // Kaju
        "Arecanut",        // Supari / Betelnut
        "Mango",           // Alphonso / Hapus
        "Turmeric",        // Haldi; kharif spice
        "Kokum(Ratamba)",
        "Pepper",          // Black pepper
        "Banana",
    };

    public List<string> IrrigationTypes { get; } = new()
    {
        "Rainfed",
        "Irrigated (Canal/Well)",
        "Drip / Sprinkler",
    };

    // Season lookup per crop
    private static readonly Dictionary<string, (string Season, string DateLabel)> CropMeta = new()
    {
        ["Paddy(Deshaj)"]  = ("Kharif",    "Sowing / Transplanting Date"),
        ["Coconut"]        = ("Perennial",  "Expected Harvest Start Date"),
        ["Cashewnuts"]     = ("Perennial",  "Flowering / Harvest Date"),
        ["Arecanut"]       = ("Perennial",  "Expected Harvest Start Date"),
        ["Mango"]          = ("Perennial",  "Flowering Start Date"),
        ["Turmeric"]       = ("Kharif",     "Sowing / Planting Date"),
        ["Kokum(Ratamba)"] = ("Perennial",  "Expected Harvest Date"),
        ["Pepper"]         = ("Perennial",  "Expected Harvest Date"),
        ["Banana"]         = ("Year-round", "Planting / Suckers Date"),
    };

    public CropDetailsViewModel(IApiService api, IOnDeviceInferenceService onDevice,
                                 ILocalDatabaseService db, IConnectivityService conn)
    {
        _api      = api;
        _onDevice = onDevice;
        _db       = db;
        _conn     = conn;
        Title     = "Crop Details";
    }

    partial void OnSelectedCropChanged(string value)
    {
        if (CropMeta.TryGetValue(value, out var meta))
        {
            Season    = meta.Season;
            DateLabel = meta.DateLabel;
        }
    }

    [RelayCommand]
    private async Task RunPredictionAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(SelectedCrop)) return;
        IsBusy = true;

        try
        {
            var date = Uri.EscapeDataString(PlantingDate.ToString("yyyy-MM-dd"));
            var crop = Uri.EscapeDataString(SelectedCrop);
            var irr  = Uri.EscapeDataString(IrrigationType);
            await Shell.Current.GoToAsync(
                $"predictionloading?fieldId={FieldId}&crop={crop}&date={date}" +
                $"&area={AreaHectares:F4}&useIot={UseIotData}&season={Season}&irrigation={irr}" +
                (UseIotData ? BuildIotQuery() : ""));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildIotQuery()
        => $"&n={SoilN}&p={SoilP}&k={SoilK}&moist={Moisture}&ph={PH}&temp={Temperature}";
}
