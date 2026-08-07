using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace dj.test.system;

public interface IDataManagement
{
    Task RestoreDefaults();
    Task SetMedia(string newMediaDatabasePath);
    Task SetTmdb(string tmdbDatabasePath);
}

public class DataManagement(IOptions<shared.data.DatabaseConfiguration> _dbConfig, ILogger<DataManagement> _logger) : IDataManagement
{
    private string DatabasePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, _dbConfig.Value.DataFile));
        }
    }

    private string ReferencePath
    {
        get
        {
            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Environment.ProcessPath is null");
            var rootDir = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Unable to determine process directory");
            return Path.GetFullPath(Path.Combine(rootDir, "backup_data/"));
        }
    }

    public async Task RestoreDefaults()
    {
        var dirInfo = new DirectoryInfo(ReferencePath);

        foreach (var file in dirInfo.EnumerateFiles())
        {
            file.CopyTo(DatabasePath, true);
        }
    }

    public async Task SetMedia(string newMediaDatabasePath)
    {
        if (!File.Exists(newMediaDatabasePath))
        {
            _logger.LogError($"Could not find database source: {newMediaDatabasePath}");
            return;
        }

        try
        {
            File.Copy(newMediaDatabasePath, DatabasePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while settings {newMediaDatabasePath} to media database:  {ex}");
        }
    }

    public async Task SetTmdb(string tmdbDatabasePath)
    {
        if (!File.Exists(tmdbDatabasePath))
        {
            _logger.LogError($"Could not find database source: {tmdbDatabasePath}");
            return;
        }

        try
        {
            File.Copy(tmdbDatabasePath, DatabasePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while settings {tmdbDatabasePath} to tmdb database:  {ex}");
        }

    }


}