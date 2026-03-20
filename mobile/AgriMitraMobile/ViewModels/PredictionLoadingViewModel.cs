using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

[QueryProperty(nameof(FieldId),       "fieldId")]
[QueryProperty(nameof(CropType),      "crop")]
[QueryProperty(nameof(PlantingDate),  "date")]
[QueryProperty(nameof(AreaHectares),  "area")]
[QueryProperty(nameof(UseIot),        "useIot")]
[QueryProperty(nameof(Season),        "season")]
[QueryProperty(nameof(IrrigationType),"irrigation")]
[QueryProperty(nameof(SoilN),         "n")]
[QueryProperty(nameof(SoilP),         "p")]
[QueryProperty(nameof(SoilK),         "k")]
[QueryProperty(nameof(Moisture),      "moist")]
[QueryProperty(nameof(PH),            "ph")]
[QueryProperty(nameof(Temperature),   "temp")]
public partial class PredictionLoadingViewModel : BaseViewModel
{
    private readonly IApiService               _api;
    private readonly IOnDeviceInferenceService _onDevice;
    private readonly ILocalDatabaseService     _db;
    private readonly IConnectivityService      _conn;

    [ObservableProperty] private int    _fieldId;
    [ObservableProperty] private string _cropType      = "Paddy(Deshaj)";
    [ObservableProperty] private string _plantingDate  = string.Empty;
    [ObservableProperty] private double _areaHectares;
    [ObservableProperty] private bool   _useIot;
    [ObservableProperty] private string _season        = "Kharif";
    [ObservableProperty] private string _irrigationType = "Rainfed";
    [ObservableProperty] private string _soilN        = "100";
    [ObservableProperty] private string _soilP        = "50";
    [ObservableProperty] private string _soilK        = "200";
    [ObservableProperty] private string _moisture     = "35";
    [ObservableProperty] private string _pH           = "6.5";
    [ObservableProperty] private string _temperature  = "28";

    [ObservableProperty] private string _statusMessage = "Connecting to server...";
    [ObservableProperty] private bool   _isOffline;

    private static readonly string[] OnlineMessages =
    {
        "Connecting to server...",
        "Fetching satellite data...",
        "Running AI model...",
        "Generating advisories...",
        "Almost done..."
    };

    private static readonly string[] OfflineMessages =
    {
        "Loading on-device model...",
        "Processing satellite images...",
        "Running ONNX inference...",
        "Generating advisories...",
        "Preparing results..."
    };

    public PredictionLoadingViewModel(IApiService api, IOnDeviceInferenceService onDevice,
                                       ILocalDatabaseService db, IConnectivityService conn)
    {
        _api      = api;
        _onDevice = onDevice;
        _db       = db;
        _conn     = conn;
        Title     = "Predicting...";
    }

    public async Task StartPredictionAsync()
    {
        IsBusy    = true;
        IsOffline = !_conn.IsConnected;
        var msgs  = IsOffline ? OfflineMessages : OnlineMessages;

        using var cts = new CancellationTokenSource();
        _ = AnimateStatusAsync(msgs, cts.Token);

        PredictionResult? result = null;
        try
        {
            if (!IsOffline)
            {
                var field = await _db.GetFieldAsync(FieldId);
                var req   = new PredictRequest(
                    FarmCoordinates: ParseGeoJsonCoords(field?.PolygonGeoJson),
                    CropType:        CropType,
                    IotSensorData:   BuildIot(),
                    PlantingDate:    PlantingDate,
                    FieldId:         FieldId.ToString()
                );
                result = await _api.PredictAsync(req);
            }

            result ??= await _onDevice.PredictAsync(CropType, BuildIotArray(), PlantingDate, Season, IrrigationType);
        }
        catch
        {
            result = await _onDevice.PredictAsync(CropType, BuildIotArray(), PlantingDate, Season, IrrigationType);
        }
        finally
        {
            cts.Cancel();
            IsBusy = false;
        }

        if (result != null)
        {
            // Save locally
            var pred = new LocalPrediction
            {
                FieldId            = FieldId,
                CropType           = result.CropType,
                PredictedYield     = result.PredictedYield,
                UncertaintyBand    = result.UncertaintyBand,
                FertilizerAdvisory = result.FertilizerAdvisory ?? string.Empty,
                IrrigationAdvisory = result.IrrigationAdvisory ?? string.Empty,
                MarketAdvisory     = result.MarketAdvisory     ?? string.Empty,
                ModelVersion       = result.ModelVersion       ?? string.Empty,
                IsOffline          = result.IsOffline,
                CreatedAt          = DateTime.UtcNow,
            };
            await _db.SavePredictionAsync(pred);

            // Navigate to result page with ID (relative route — registered via Routing.RegisterRoute)
            await Shell.Current.GoToAsync($"predictionresult?predId={pred.Id}");
        }
    }

    private async Task AnimateStatusAsync(string[] messages, CancellationToken ct)
    {
        int i = 0;
        while (!ct.IsCancellationRequested)
        {
            StatusMessage = messages[i % messages.Length];
            i++;
            try { await Task.Delay(1200, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private IoTSensorData? BuildIot()
    {
        if (!UseIot) return null;
        return new IoTSensorData(
            Parse(SoilN), Parse(SoilP), Parse(SoilK),
            Parse(Moisture), Parse(PH), Parse(Temperature));
    }

    private float[]? BuildIotArray()
    {
        if (!UseIot) return null;
        return new[] { Parse(SoilN), Parse(SoilP), Parse(SoilK),
                        Parse(Moisture), Parse(PH), Parse(Temperature), 0.6f, 100f };
    }

    private static float Parse(string s) => float.TryParse(s, out float v) ? v : 0f;

    private static List<List<double>> ParseGeoJsonCoords(string? geoJson)
    {
        if (string.IsNullOrEmpty(geoJson)) return new();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(geoJson);
            var ring = doc.RootElement
                         .GetProperty("coordinates")[0]
                         .EnumerateArray()
                         .Select(pt => new List<double>
                             { pt[0].GetDouble(), pt[1].GetDouble() })
                         .ToList();
            return ring;
        }
        catch { return new(); }
    }
}
