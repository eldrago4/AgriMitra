using AgriMitraMobile.ViewModels;

namespace AgriMitraMobile.Views;

public partial class PredictionResultPage : ContentPage
{
    private readonly PredictionResultViewModel _vm;

    public PredictionResultPage(PredictionResultViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
