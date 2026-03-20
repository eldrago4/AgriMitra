using AgriMitraMobile.ViewModels;

namespace AgriMitraMobile.Views;

public partial class MyFieldsPage : ContentPage
{
    private readonly MyFieldsViewModel _vm;

    public MyFieldsPage(MyFieldsViewModel vm)
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
