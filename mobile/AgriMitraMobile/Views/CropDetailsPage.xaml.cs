using AgriMitraMobile.ViewModels;

namespace AgriMitraMobile.Views;

public partial class CropDetailsPage : ContentPage
{
    public CropDetailsPage(CropDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
