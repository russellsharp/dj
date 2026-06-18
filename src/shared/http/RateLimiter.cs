

using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimit;
using RestSharp;
using shared.TMDB;

namespace shared.http;

public interface IRateLimiter
{
    Task<RestResponse> Get(RestRequest request, CancellationToken token);
    string BuildUri(RestRequest request);
}

public class RateLimiter : IRateLimiter
{
    private readonly RestClient _client;
    private readonly AsyncRateLimitPolicy _rateLimitPolicy;
    private readonly EndpointConfig _config;
    public RateLimiter(IOptions<EndpointConfig> options)
    {

        _config = options.Value;

        _client = new RestClient(_config.BaseUrl);

        _rateLimitPolicy = Policy.RateLimitAsync(_config.RequestLimit, TimeSpan.FromSeconds(_config.RequestWindowSeconds));
    }

    public async Task<RestResponse> Get(RestRequest request, CancellationToken token)
    {
        int attemptCount = 0;
        int retryBackoffGrowth = 1000;
        while (attemptCount < _config.AttemptCountMax && !token.IsCancellationRequested)
        {
            attemptCount++;
            try
            {
                // This will block or wait until the rate allows the request
                return await _rateLimitPolicy.ExecuteAsync(async () =>
                {
                    return await _client.GetAsync(request);
                });
            }
            catch (Polly.RateLimit.RateLimitRejectedException rateLimitReachedEx)
            {
                Debug.WriteLine($"Retry after: {_config.BackOffTimeMs + (retryBackoffGrowth * attemptCount)}");
                await Task.Delay(_config.BackOffTimeMs + (retryBackoffGrowth * attemptCount));
            }
        }

        return new RestResponse() { ResponseStatus = ResponseStatus.Error, StatusCode = System.Net.HttpStatusCode.TooManyRequests };
    }

    public string BuildUri(RestRequest request)
    {
        return _client.BuildUri(request).ToString();
    }
}