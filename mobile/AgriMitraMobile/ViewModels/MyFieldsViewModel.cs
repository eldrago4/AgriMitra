using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

public partial class FieldSummary
{
    public int    Id          { get; set; }
    public string Label       { get; set; } = string.Empty;
    public string AreaText    { get; set; } = string.Empty;
    public string LastCrop    { get; set; } = string.Empty;
    public string LastYield   { get; set; } = string.Empty;
    public string LastDate    { get; set; } = string.Empty;
    public bool   HasPred     { get; set; }
}

public partial class MyFieldsViewModel : BaseViewModel
{
    private readonly ILocalDatabaseService _db;

    [ObservableProperty] private bool _isEmpty;

    public ObservableCollection<FieldSummary> Fields { get; } = new();

    public MyFieldsViewModel(ILocalDatabaseService db)
    {
        _db   = db;
        Title = "My Fields";
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        Fields.Clear();
        try
        {
            var fields = await _db.GetAllFieldsAsync();
            foreach (var f in fields)
            {
                var lastPred = await _db.GetLatestPredictionForFieldAsync(f.Id);
                Fields.Add(new FieldSummary
                {
                    Id       = f.Id,
                    Label    = f.Label,
                    AreaText = $"{f.AreaHectares:F2} ha",
                    LastCrop  = lastPred?.CropType ?? string.Empty,
                    LastYield = lastPred != null ? $"{lastPred.PredictedYield:F1} q/ha" : string.Empty,
                    LastDate  = lastPred?.CreatedAt.ToString("dd MMM yyyy") ?? string.Empty,
                    HasPred   = lastPred != null,
                });
            }
            IsEmpty = Fields.Count == 0;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddFieldAsync()
        => await Shell.Current.GoToAsync("farmmap");

    [RelayCommand]
    private async Task OpenFieldAsync(FieldSummary? summary)
    {
        if (summary == null) return;
        await Shell.Current.GoToAsync($"cropdetails?fieldId={summary.Id}&area={summary.AreaText.Replace(" ha","")}");
    }

    [RelayCommand]
    private async Task DeleteFieldAsync(FieldSummary? summary)
    {
        if (summary == null) return;
        bool confirmed = await Shell.Current.DisplayAlert(
            "Delete Field", $"Delete \"{summary.Label}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        await _db.DeleteFieldAsync(summary.Id);
        Fields.Remove(summary);
        IsEmpty = Fields.Count == 0;
    }
}
