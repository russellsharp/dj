using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
namespace dj.test.system;

[CollectionDefinition("WebAppCollection")]
public class WebAppCollection : ICollectionFixture<WebAppFixture> { }


public class WebAppFixture : BaseFixture
{
    public WebApplicationFactory<Program> Application;

    public override IServiceProvider Services
    {
        get => Application.Services;
        protected set => throw new InvalidOperationException("Cannot set Services for this fixture.");
    }

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