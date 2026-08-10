namespace shared.http;

public class RateLimiterConfiguration
{
    public int PermitLimit { get; init; } = 1;
    public int WindowSeconds { get; init; } = 5;
    public string QueueProcessingOrder { get; init; } = "OldestFirst";
    public int QueueLimit { get; init; } = 1;
}

