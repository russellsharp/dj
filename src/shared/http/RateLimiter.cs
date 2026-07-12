

using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.RateLimit;
using Polly.Retry;
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
    private AsyncRetryPolicy _retryPolicy;
    private Polly.Wrap.AsyncPolicyWrap _pipeline;

    public RateLimiter(IOptions<EndpointConfig> options)
    {

        _config = options.Value;

        _client = new RestClient(_config.BaseUrl);

        var rateLimitPolicy = Policy.RateLimitAsync(1, TimeSpan.FromSeconds(10), 1);

        var retryPolicy = Policy
            .Handle<RateLimitRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: (retryCount, exception, context) =>
                {
                    // If rejected, wait exactly what the rate limiter says is left in the window
                    if (exception is RateLimitRejectedException rateLimitEx)
                    {
                        return rateLimitEx.RetryAfter;
                    }
                    return TimeSpan.FromSeconds(10); // Fallback
                },
                onRetryAsync: async (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine("onretry");
                }
        );

        _pipeline = Policy.WrapAsync(retryPolicy, rateLimitPolicy);

    }

    public async Task<RestResponse> Get(RestRequest request, CancellationToken token)
    {
        int attemptCount = 0;
        int retryBackoffGrowth = 1000;
        while (attemptCount < _config.AttemptCountMax && !token.IsCancellationRequested)
        {
            // await Task.Delay(5000, token);
            attemptCount++;
            try
            {
                return await _pipeline.ExecuteAsync(async () =>
                {
                    // await Task.Delay(2000, token);
                    return await _client.GetAsync(request);
                });
            }
            catch (Polly.RateLimit.RateLimitRejectedException ex)
            {
                Debug.WriteLine($"Retry after: {_config.BackOffTimeMs + (retryBackoffGrowth * attemptCount)}: \n{ex}");
                await Task.Delay(_config.BackOffTimeMs + (retryBackoffGrowth * attemptCount), token);
            }
        }

        return new RestResponse() { ResponseStatus = ResponseStatus.Error, StatusCode = System.Net.HttpStatusCode.TooManyRequests };
    }

    public string BuildUri(RestRequest request)
    {
        return _client.BuildUri(request).ToString();
    }
}
