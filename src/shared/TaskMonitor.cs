using System.Collections.Concurrent;

namespace shared;

public interface ITaskMonitor<T>
{
    void Set(Guid id, Task<T> task, CancellationTokenSource cts);
    TaskStatus? Status(Guid id);
    bool TaskExists(Guid id);
    void CancelRequest(Guid id);
}

public interface ITaskMonitor
{
    void CancelRequest(Guid id);
    void Set(Guid id, Task task, CancellationTokenSource cts);
    TaskStatus? Status(Guid id);
    bool TaskExists(Guid id);
}

public class TaskMonitor<T>() : ITaskMonitor<T>
{
    private readonly ConcurrentDictionary<Guid, object> _locks = new();

    private volatile ConcurrentDictionary<Guid, TaskContext> _tasks = new();
    public void Set(Guid id, Task<T> task, CancellationTokenSource cts)
    {
        var lockObject = _locks.GetOrAdd(id, _ => new());
        lock (lockObject)
        {
            if (_tasks.ContainsKey(id))
            {
                throw new TaskExistsException($"Task {id} is already stored in monitor.");
            }

            _tasks.TryAdd(id, new TaskContext
            {
                Monitored = task,
                Cts = cts,
            });
        }
    }

    public TaskStatus? Status(Guid id)
    {
        var lockObject = _locks.GetOrAdd(id, _ => new());
        lock (lockObject)
        {
            if (_tasks.TryGetValue(id, out var task))
            {
                return task?.Monitored?.Status;
            }
            else
            {
                throw new TaskDoesNotExist($"Attempting to get status of task {id} that does not exist.");
            }
        }
    }

    public bool TaskExists(Guid id) { return _tasks.ContainsKey(id); }

    public void CancelRequest(Guid id)
    {
        var lockObject = _locks.GetOrAdd(id, _ => new());
        lock (lockObject)
        {
            if (_tasks.TryGetValue(id, out var task))
            {
                task.Cts.Cancel();
            }
            else
            {
                throw new TaskDoesNotExist($"Attempting to cancel task {id} that does not exist.");
            }
        }
    }
}

public class TaskMonitor() : ITaskMonitor
{
    private readonly ConcurrentDictionary<Guid, object> _locks = new();
    private volatile ConcurrentDictionary<Guid, TaskContext> _tasks = new();

    public void Set(Guid id, Task task, CancellationTokenSource cts)
    {
        var lockObject = _locks.GetOrAdd(id, _ => new());
        lock (lockObject)
        {
            _tasks[id] = new TaskContext
            {
                Id = id,
                Monitored = task,
                Cts = cts,
            };
        }
    }

    public TaskStatus? Status(Guid id)
    {
        var lockObject = _locks.GetOrAdd(id, _ => new());
        lock (lockObject)
        {
            return _tasks.TryGetValue(id, out var task) ? task?.Monitored?.Status : null;
        }
    }

    public bool TaskExists(Guid id) { return _tasks.ContainsKey(id); }

    public void CancelRequest(Guid id)
    {
        var lockObject = _locks.GetOrAdd(id, _ => new());
        lock (lockObject)
        {
            if (_tasks.TryGetValue(id, out var task))
            {
                task.Cts.Cancel();
            }
            else
            {
                throw new TaskDoesNotExist($"Attempting to cancel task {id} that does not exist.");
            }
        }
    }
}

public class TaskExistsException : Exception
{
    public TaskExistsException(string msg) : base(msg) { }
}

public class TaskDoesNotExist : Exception
{
    public TaskDoesNotExist(string msg) : base(msg) { }
}

public class TaskContext
{
    public Guid Id;
    public Task? Monitored = null;
    public CancellationTokenSource Cts;
}
