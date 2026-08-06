namespace dj.test.system;

public interface ISystemFixture
{
    CancellationTokenSource Cts { get; }
    HttpClient Client { get; }
    IServiceProvider Services { get; }
    Task<HttpResponseMessage?> Get(string endpoint, Dictionary<string, string>? parameters = null, string? token = null);
    Task Initialize();

}
