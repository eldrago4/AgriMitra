using AgriMitraMobile.ViewModels;

namespace AgriMitraMobile.Views;

public partial class PredictionLoadingPage : ContentPage
{
    private readonly PredictionLoadingViewModel _vm;

    public PredictionLoadingPage(PredictionLoadingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.StartPredictionAsync();
    }
}
