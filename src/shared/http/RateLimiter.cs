using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
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
    private readonly TMDBConfiguration _config;
    private ResiliencePipeline<RestResponse> _pipeline;
    private ILogger<RateLimiter> _logger;
    private static readonly ResiliencePropertyKey<ILogger<RateLimiter>> LoggerKey = new("RateLimiterLogger");

    public RateLimiter(IOptions<TMDBConfiguration> options, ILogger<RateLimiter> logger)
    {

        _config = options.Value;

        _client = new RestClient(_config.BaseUrl);

        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder<RestResponse>()
                .AddRetry(new RetryStrategyOptions<RestResponse>
                {
                    ShouldHandle = new PredicateBuilder<RestResponse>()
                        .HandleResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        .HandleResult(response => !response.IsSuccessful && response.ErrorException != null),


                    MaxRetryAttempts = 10,

                    UseJitter = true,

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

                    OnRetry = static (args) =>
                    {
                        if (args.Context.Properties.TryGetValue(LoggerKey, out var logger))
                        {
                            logger.LogWarning(args.Outcome.Exception, $"Retry attempt: {args.AttemptNumber + 1}\r\nException: {args.Outcome.Exception?.Message}\r\nWaiting: {args.RetryDelay.TotalSeconds}");
                        }
                        else
                        {
                            Console.WriteLine($"Retry attempt: {args.AttemptNumber + 1}\r\nException: {args.Outcome.Exception?.Message}\r\nWaiting: {args.RetryDelay.TotalSeconds}");
                        }
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
    }

    public async Task<RestResponse> Get(RestRequest request, CancellationToken token)
    {
        var context = ResilienceContextPool.Shared.Get();

        context.Properties.Set(LoggerKey, _logger);

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
            _logger.LogError($"Retry after: {ex.RetryAfter}\r\n{ex.InnerException}");
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }

        return new RestResponse() { ResponseStatus = ResponseStatus.Error, StatusCode = System.Net.HttpStatusCode.TooManyRequests };
    }

    public string BuildUri(RestRequest request)
    {
        return _client.BuildUri(request).ToString();
    }
}
