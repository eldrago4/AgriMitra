using AgriMitraMobile.ViewModels;

namespace AgriMitraMobile.Views;

public partial class RegistrationPage : ContentPage
{
    private readonly RegistrationViewModel _vm;

    public RegistrationPage(RegistrationViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        SelectPill("mr");
    }

    private void OnLanguageTapped(object? sender, TappedEventArgs e)
    {
        string lang = e.Parameter as string ?? "mr";
        _vm.SetLanguage(lang);
        SelectPill(lang);
    }

    private void SelectPill(string lang)
    {
        var green = (Color)Application.Current!.Resources["PrimaryGreen"];
        var white = Colors.White;

        void Set(Border pill, Label lbl, bool selected)
        {
            pill.BackgroundColor = selected ? green : white;
            lbl.TextColor        = selected ? white : green;
        }

        Set(PillMr, LblMr, lang == "mr");
        Set(PillHi, LblHi, lang == "hi");
        Set(PillEn, LblEn, lang == "en");
    }
}
