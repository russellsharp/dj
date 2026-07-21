

using System;
using System.Buffers.Text;
using System.ComponentModel;
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
    private readonly EndpointConfig _config;
    private Polly.ResiliencePipeline<RestResponse> _pipeline;

    public RateLimiter(IOptions<EndpointConfig> options)
    {

        _config = options.Value;

        _client = new RestClient(_config.BaseUrl);

        _pipeline = new ResiliencePipelineBuilder<RestResponse>()
                .AddRetry(new RetryStrategyOptions<RestResponse>
                {
                    ShouldHandle = new PredicateBuilder<RestResponse>()
                        .HandleResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        .HandleResult(response => !response.IsSuccessful && response.ErrorException != null),

                    MaxRetryAttempts = _config.AttemptCountMax,

                    DelayGenerator = static args =>
                    {
                        var response = args.Outcome.Result;

                        var retryHeader = response?.Headers?.FirstOrDefault(h => h.Name?.Equals("Retry-After", StringComparison.OrdinalIgnoreCase) == true);
                        if (retryHeader?.Value is string headerValue && double.TryParse(headerValue, out var retryInSeconds))
                        {
                            return ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(retryInSeconds));
                        }
                        return ValueTask.FromResult<TimeSpan?>(null);
                    },

                    BackoffType = DelayBackoffType.Exponential,

                    OnRetry = static args =>
                    {
                        Console.WriteLine($"Retry attempt: {args.AttemptNumber + 1}\r\nException: {args.Outcome.Exception?.Message}\r\nWaiting: {args.RetryDelay.TotalSeconds}");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
    }

    public async Task<RestResponse> Get(RestRequest request, CancellationToken token)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async cancellationToken =>
                {
                    return await _client.GetAsync(request, cancellationToken);
                },
                token
            );
        }
        catch (Polly.RateLimit.RateLimitRejectedException ex)
        {
            Console.WriteLine($"Retry after: {ex.RetryAfter}\r\n{ex.InnerException}");
        }

        return new RestResponse() { ResponseStatus = ResponseStatus.Error, StatusCode = System.Net.HttpStatusCode.TooManyRequests };
    }

    public string BuildUri(RestRequest request)
    {
        return _client.BuildUri(request).ToString();
    }
}
