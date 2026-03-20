using SQLite;
using AgriMitraMobile.Models;

namespace AgriMitraMobile.Services;

public class LocalDatabaseService : ILocalDatabaseService
{
    private SQLiteAsyncConnection? _db;
    private static readonly string DbPath =
        Path.Combine(FileSystem.AppDataDirectory, "agrimitra.db3");

    public async Task InitAsync()
    {
        if (_db is not null) return;
        _db = new SQLiteAsyncConnection(DbPath, SQLiteOpenFlags.ReadWrite |
                                                 SQLiteOpenFlags.Create |
                                                 SQLiteOpenFlags.SharedCache);
        await _db.CreateTableAsync<LocalField>();
        await _db.CreateTableAsync<LocalPrediction>();
    }

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db is null) await InitAsync();
        return _db!;
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    public async Task<List<LocalField>> GetFieldsAsync()
    {
        var db = await GetDbAsync();
        return await db.Table<LocalField>().OrderByDescending(f => f.CreatedAt).ToListAsync();
    }

    public Task<List<LocalField>> GetAllFieldsAsync() => GetFieldsAsync();

    public async Task<LocalField?> GetFieldAsync(int id)
    {
        var db = await GetDbAsync();
        return await db.FindAsync<LocalField>(id);
    }

    public async Task<int> SaveFieldAsync(LocalField field)
    {
        var db = await GetDbAsync();
        return field.Id == 0
            ? await db.InsertAsync(field)
            : await db.UpdateAsync(field);
    }

    public async Task DeleteFieldAsync(int id)
    {
        var db = await GetDbAsync();
        await db.DeleteAsync<LocalField>(id);
        // Delete associated predictions
        var preds = await db.Table<LocalPrediction>().Where(p => p.FieldId == id).ToListAsync();
        foreach (var p in preds) await db.DeleteAsync(p);
    }

    // ── Predictions ───────────────────────────────────────────────────────────

    public async Task<List<LocalPrediction>> GetPredictionsForFieldAsync(int fieldId)
    {
        var db = await GetDbAsync();
        return await db.Table<LocalPrediction>()
                       .Where(p => p.FieldId == fieldId)
                       .OrderByDescending(p => p.CreatedAt)
                       .ToListAsync();
    }

    public async Task<LocalPrediction?> GetLatestPredictionAsync()
    {
        var db = await GetDbAsync();
        return await db.Table<LocalPrediction>()
                       .OrderByDescending(p => p.CreatedAt)
                       .FirstOrDefaultAsync();
    }

    public async Task<LocalPrediction?> GetLatestPredictionForFieldAsync(int fieldId)
    {
        var db = await GetDbAsync();
        return await db.Table<LocalPrediction>()
                       .Where(p => p.FieldId == fieldId)
                       .OrderByDescending(p => p.CreatedAt)
                       .FirstOrDefaultAsync();
    }

    public async Task<LocalPrediction?> GetPredictionAsync(int id)
    {
        var db = await GetDbAsync();
        return await db.FindAsync<LocalPrediction>(id);
    }

    public async Task<int> SavePredictionAsync(LocalPrediction pred)
    {
        var db = await GetDbAsync();
        return pred.Id == 0
            ? await db.InsertAsync(pred)
            : await db.UpdateAsync(pred);
    }

    // ── Maintenance ───────────────────────────────────────────────────────────

    public async Task ClearAllAsync()
    {
        var db = await GetDbAsync();
        await db.DeleteAllAsync<LocalPrediction>();
        await db.DeleteAllAsync<LocalField>();
    }
}
