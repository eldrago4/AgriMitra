using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

public partial class RegistrationViewModel : BaseViewModel
{
    private readonly IApiService _api;

    [ObservableProperty] private string _name     = string.Empty;
    [ObservableProperty] private string _phone    = string.Empty;
    [ObservableProperty] private string _district = string.Empty;
    [ObservableProperty] private string _village  = string.Empty;
    [ObservableProperty] private string _language = "mr";

    public List<string> Districts { get; } = new()
    {
        "Sindhudurg", "Kolhapur", "Sangli", "Pune", "Nashik",
        "Aurangabad", "Nagpur", "Latur", "Solapur", "Ratnagiri"
    };

    public List<string> Languages { get; } = new() { "mr", "hi", "en" };
    public List<string> LanguageLabels { get; } = new() { "मराठी", "हिन्दी", "English" };

    public RegistrationViewModel(IApiService api)
    {
        _api  = api;
        Title = "Registration";
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Phone))
        {
            await Shell.Current.DisplayAlert("Required", "Please enter name and phone number.", "OK");
            return;
        }
        if (Phone.Length < 10)
        {
            await Shell.Current.DisplayAlert("Invalid", "Please enter a valid 10-digit phone number.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var profile = new FarmerProfile
            {
                Name     = Name.Trim(),
                Phone    = Phone.Trim(),
                Village  = Village,
                District = District,
                Language = Language,
            };

            // Try to register on server (non-blocking — app works offline too)
            var serverId = await _api.RegisterFarmerAsync(profile);
            if (serverId != null) profile.ServerId = serverId;

            profile.Save();

            await Shell.Current.GoToAsync("//home");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        // Check if already registered
        var profile = FarmerProfile.Load();
        if (profile.IsRegistered)
            await Shell.Current.GoToAsync("//home");
        else
            await Shell.Current.DisplayAlert("Not found", "No account found. Please register.", "OK");
    }

    public void SetLanguage(string lang) => Language = lang;
}
