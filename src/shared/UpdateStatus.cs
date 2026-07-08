namespace shared;

public enum UpdateState
{
    NotStarted,
    Running,
    Canceled,
    Complete,
    Errored
}

public record UpdateStatus
{
    public UpdateState State { get; set; } = UpdateState.NotStarted;
    public long FilesProcessed { get; set; } = 0;
    public long TotalFiles { get; set; } = 0;
}
