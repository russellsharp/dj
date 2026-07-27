using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace dj.test.system;

[CollectionDefinition("WebAppCollection")]
public class WebAppCollection : ICollectionFixture<WebAppFixture> { }


public class WebAppFixture : BaseFixture
{
    public WebApplicationFactory<Program> Application;

    public override async Task Initialize()
    {
        Application = new TestWebApplicationFactory<Program>();

        Client = Application.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        Cts = Application.Services.GetRequiredService<CancellationTokenSource>();

        await base.Initialize();
    }
}

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
        });

        builder.ConfigureTestServices(services =>
        {
            var originalCtsEntry = services.Single(x => x.ServiceType == typeof(CancellationTokenSource));
            services.Remove(originalCtsEntry);

            var cts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestContext.Current.CancellationToken);

            services.AddSingleton(linkedCts);

            services.AddSingleton<IDataManagement, DataManagement>();
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
    }
}

public interface IDataManagement
{
    Task RestoreDefaults();
    Task SetMedia(string newMediaDatabasePath);
    Task SetTmdb(string tmdbDatabasePath);
}

public class DataManagement(IOptions<shared.data.DatabaseConfiguration> _dbConfig) : IDataManagement
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
            Console.WriteLine($"Could not find database source: {newMediaDatabasePath}");
            return;
        }

        try
        {
            File.Copy(newMediaDatabasePath, DatabasePath, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while settings {newMediaDatabasePath} to media database:  {ex}");
        }
    }

    public async Task SetTmdb(string tmdbDatabasePath)
    {
        if (!File.Exists(tmdbDatabasePath))
        {
            Console.WriteLine($"Could not find database source: {tmdbDatabasePath}");
            return;
        }

        try
        {
            File.Copy(tmdbDatabasePath, DatabasePath, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while settings {tmdbDatabasePath} to tmdb database:  {ex}");
        }

    }


}