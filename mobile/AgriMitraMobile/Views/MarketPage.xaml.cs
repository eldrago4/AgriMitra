using AgriMitraMobile.ViewModels;

namespace AgriMitraMobile.Views;

public partial class MarketPage : ContentPage
{
    private readonly MarketViewModel _vm;

    public MarketPage(MarketViewModel vm)
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
