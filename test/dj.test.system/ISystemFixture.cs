namespace dj.test.system;

public interface ISystemFixture
{
    HttpClient Client { get; }
    Task<HttpResponseMessage?> Get(string endpoint, Dictionary<string, string>? parameters = null, string? token = null);
    Task Initialize();
}
