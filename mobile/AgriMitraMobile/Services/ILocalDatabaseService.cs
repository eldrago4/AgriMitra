using AgriMitraMobile.Models;

namespace AgriMitraMobile.Services;

public interface ILocalDatabaseService
{
    Task InitAsync();
    Task<List<LocalField>>      GetFieldsAsync();
    Task<List<LocalField>>      GetAllFieldsAsync();
    Task<LocalField?>           GetFieldAsync(int id);
    Task<int>                   SaveFieldAsync(LocalField field);
    Task                        DeleteFieldAsync(int id);
    Task<List<LocalPrediction>> GetPredictionsForFieldAsync(int fieldId);
    Task<LocalPrediction?>      GetLatestPredictionAsync();
    Task<LocalPrediction?>      GetLatestPredictionForFieldAsync(int fieldId);
    Task<LocalPrediction?>      GetPredictionAsync(int id);
    Task<int>                   SavePredictionAsync(LocalPrediction pred);
    Task                        ClearAllAsync();
}
