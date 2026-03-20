using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

public partial class HomeViewModel : BaseViewModel
{
    private readonly ILocalDatabaseService _db;

    [ObservableProperty] private string _greeting      = "Namaste!";
    [ObservableProperty] private string _lastCropName  = string.Empty;
    [ObservableProperty] private string _lastYield     = string.Empty;
    [ObservableProperty] private string _lastPredDate  = string.Empty;
    [ObservableProperty] private bool   _hasLastPred   = false;

    public HomeViewModel(ILocalDatabaseService db)
    {
        _db   = db;
        Title = "Home";
    }

    public async Task LoadAsync()
    {
        var profile = FarmerProfile.Load();
        if (!profile.IsRegistered)
        {
            await Shell.Current.GoToAsync("//registration");
            return;
        }
        Greeting = $"Namaste, {profile.Name.Split(' ')[0]}!";

        var lastPred = await _db.GetLatestPredictionAsync();
        if (lastPred != null)
        {
            HasLastPred  = true;
            LastCropName = lastPred.CropType;
            LastYield    = $"{lastPred.PredictedYield:F1} q/ha";
            LastPredDate = lastPred.CreatedAt.ToString("dd MMM yyyy");
        }
    }

    [RelayCommand] private async Task NewPredictionAsync()
        => await Shell.Current.GoToAsync("farmmap");

    [RelayCommand] private async Task MyFieldsAsync()
        => await Shell.Current.GoToAsync("//myfields");

    [RelayCommand] private async Task MarketAsync()
        => await Shell.Current.GoToAsync("//market");

    [RelayCommand] private async Task ViewLastPredictionAsync()
        => await Shell.Current.GoToAsync("//myfields");
}
