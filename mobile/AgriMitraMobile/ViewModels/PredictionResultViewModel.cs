using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

[QueryProperty(nameof(PredId), "predId")]
public partial class PredictionResultViewModel : BaseViewModel
{
    private readonly ILocalDatabaseService _db;
    private LocalPrediction? _pred;

    [ObservableProperty] private int    _predId;
    [ObservableProperty] private string _cropType           = string.Empty;
    [ObservableProperty] private string _yieldText          = string.Empty;
    [ObservableProperty] private string _uncertaintyText    = string.Empty;
    [ObservableProperty] private string _fertilizerAdvisory = string.Empty;
    [ObservableProperty] private string _irrigationAdvisory = string.Empty;
    [ObservableProperty] private string _marketAdvisory     = string.Empty;
    [ObservableProperty] private string _modelVersion       = string.Empty;
    [ObservableProperty] private string _dateText           = string.Empty;
    [ObservableProperty] private bool   _isOffline;
    [ObservableProperty] private double _yieldBarWidth      = 200;

    // Benchmark comparisons
    [ObservableProperty] private double _benchmarkBarWidth  = 150;
    [ObservableProperty] private string _benchmarkText      = string.Empty;

    // District/national average yields (q/ha) used for benchmark bar
    private static readonly Dictionary<string, float> NationalAvgYield = new()
    {
        // Sindhudurg / Konkan crops
        ["Paddy(Deshaj)"]  = 16.0f,
        ["Coconut"]        = 35.0f,
        ["Cashewnuts"]     = 8.0f,
        ["Arecanut"]       = 18.0f,
        ["Mango"]          = 75.0f,
        ["Turmeric"]       = 40.0f,
        ["Kokum(Ratamba)"] = 22.0f,
        ["Pepper"]         = 4.0f,
        ["Banana"]         = 180.0f,
        // Legacy
        ["Rice"]           = 16.0f,
        ["Wheat"]          = 17.5f,
        ["Soybean"]        = 11.2f,
        ["Sugarcane"]      = 600.0f,
    };

    public PredictionResultViewModel(ILocalDatabaseService db)
    {
        _db   = db;
        Title = "Prediction Result";
    }

    public async Task LoadAsync()
    {
        _pred = await _db.GetPredictionAsync(PredId);
        if (_pred == null) return;

        CropType           = _pred.CropType;
        YieldText          = $"{_pred.PredictedYield:F1} q/ha";
        UncertaintyText    = $"± {_pred.UncertaintyBand:F1} q/ha";
        FertilizerAdvisory = _pred.FertilizerAdvisory;
        IrrigationAdvisory = _pred.IrrigationAdvisory;
        MarketAdvisory     = _pred.MarketAdvisory;
        ModelVersion       = _pred.ModelVersion;
        DateText           = _pred.CreatedAt.ToString("dd MMM yyyy, HH:mm");
        IsOffline          = _pred.IsOffline;

        float national = NationalAvgYield.GetValueOrDefault(_pred.CropType, 15f);
        float ratio    = (float)(_pred.PredictedYield / national);
        YieldBarWidth     = Math.Min(300, ratio * 200);
        BenchmarkBarWidth = 200;
        BenchmarkText     = ratio >= 1.05 ? $"+{(ratio-1)*100:F0}% above national avg"
                          : ratio >= 0.95 ? "At national avg"
                          :                 $"{(1-ratio)*100:F0}% below national avg";
    }

    [RelayCommand]
    private async Task SaveResultAsync()
    {
        // Already saved in PredictionLoadingViewModel; just confirm to user
        await Shell.Current.DisplayAlert("Saved",
            "Prediction saved to My Fields.", "OK");
    }

    [RelayCommand]
    private async Task ShareViaSmsAsync()
    {
        if (_pred == null) return;
        string msg = $"AgriMitra Prediction\n" +
                     $"Crop: {_pred.CropType}\n" +
                     $"Yield: {_pred.PredictedYield:F1} q/ha\n" +
                     $"{_pred.MarketAdvisory}";
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text  = msg,
            Title = "Share Prediction"
        });
    }

    [RelayCommand]
    private async Task NewPredictionAsync()
        => await Shell.Current.GoToAsync("//home");

    [RelayCommand]
    private async Task ViewMyFieldsAsync()
        => await Shell.Current.GoToAsync("//myfields");
}
