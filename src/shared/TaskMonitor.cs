using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace shared;

public interface ITaskMonitor<T>
{
    void Set(Task<T> task, CancellationTokenSource cts);
    TaskStatus? Status { get; }
    bool TaskExists { get; }
    void CancelRequest();
}

public class TaskMonitor<T>(CancellationTokenSource? cts) : ITaskMonitor<T>
{
    private CancellationTokenSource? _cts;

    private volatile Task<T>? _task = null;

    private object _lock = new();

    public void Set(Task<T> task, CancellationTokenSource cts)
    {
        lock (_lock)
        {
            Console.WriteLine($"[Monitor] Task registered. HashCode: {this.GetHashCode()}");

            _task = task;

            _cts = cts;
        }
    }

    public TaskStatus? Status
    {
        get
        {
            lock (_lock)
            {
                Console.WriteLine($"[Monitor] Task registered. HashCode: {this.GetHashCode()}");
                Console.WriteLine($"task is canceled: {_task.IsCanceled}");
                return _task?.Status;
            }
        }
    }
    public bool TaskExists { get { return _task is not null; } }

    public void CancelRequest()
    {
        lock (_lock)
        {
            _cts?.Cancel();
        }
    }
}
public interface ITaskMonitor
{
    void Set(Task task, CancellationTokenSource cts);
    TaskStatus? Status { get; }
    bool TaskExists { get; }
    void CancelRequest();
}

public class TaskMonitor() : ITaskMonitor
{
    private readonly object _lock = new object();
    private volatile Task? _task = null;
    private CancellationTokenSource _cts;

    public void Set(Task task, CancellationTokenSource cts)
    {
        lock (_lock)
        {
            _task = task;
            _cts = cts;
        }
    }

    public TaskStatus? Status
    {
        get
        {
            lock (_lock)
            {
                return _task?.Status;
            }
        }
    }
    public bool TaskExists { get { return _task is not null; } }

    public void CancelRequest()
    {
        lock (_lock)
        {
            _cts?.Cancel();
        }
    }
}
