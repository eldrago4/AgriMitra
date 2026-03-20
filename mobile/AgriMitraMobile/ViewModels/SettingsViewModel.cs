using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IOnDeviceInferenceService _onDevice;
    private readonly ILocalDatabaseService     _db;

    [ObservableProperty] private string _selectedLanguage  = "mr";
    public int SelectedLanguageIndex
    {
        get => Languages.IndexOf(SelectedLanguage);
        set { if (value >= 0) SelectedLanguage = Languages[value]; }
    }
    [ObservableProperty] private bool   _smsOptIn;
    [ObservableProperty] private string _phoneNumber       = string.Empty;
    [ObservableProperty] private string _farmerName        = string.Empty;
    [ObservableProperty] private string _district          = string.Empty;
    [ObservableProperty] private string _modelStatus       = "Checking...";
    [ObservableProperty] private string _appVersion        = "1.0.0";

    public List<string> Languages      { get; } = new() { "mr", "hi", "en" };
    public List<string> LanguageLabels { get; } = new() { "मराठी", "हिन्दी", "English" };

    public SettingsViewModel(IOnDeviceInferenceService onDevice, ILocalDatabaseService db)
    {
        _onDevice = onDevice;
        _db       = db;
        Title     = "Settings";
    }

    public void Load()
    {
        var profile = FarmerProfile.Load();
        FarmerName       = profile.Name;
        PhoneNumber      = profile.Phone;
        District         = profile.District;
        SelectedLanguage = Preferences.Default.Get("language", "mr");
        SmsOptIn         = Preferences.Default.Get("sms_opt_in", false);
        ModelStatus      = _onDevice.IsModelLoaded
            ? "agrimitra_int8_v1.0  ✓ Loaded"
            : "Model not loaded — tap to download";
        AppVersion       = AppInfo.Current.VersionString;
    }

    [RelayCommand]
    private void SaveLanguage()
    {
        Preferences.Default.Set("language", SelectedLanguage);
        Shell.Current.DisplayAlert("Saved", "Language preference saved. Restart to apply.", "OK");
    }

    [RelayCommand]
    private void ToggleSms()
    {
        Preferences.Default.Set("sms_opt_in", SmsOptIn);
    }

    [RelayCommand]
    private async Task UpdateModelAsync()
    {
        await Shell.Current.DisplayAlert("Model Update",
            "Model is bundled with the app. To update, install the latest version from the store.", "OK");
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Clear Cache", "Delete all offline predictions and field data?", "Clear", "Cancel");
        if (!confirmed) return;

        await _db.ClearAllAsync();
        await Shell.Current.DisplayAlert("Done", "Cache cleared.", "OK");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Logout", "Remove your profile from this device?", "Logout", "Cancel");
        if (!confirmed) return;

        FarmerProfile.Clear();
        await Shell.Current.GoToAsync("//registration");
    }
}
