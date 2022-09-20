namespace TestProject;

class AsyncPool<T, TState> : IAsyncPool<T, TState>
{
    private readonly object _lockObject = new();
    private readonly IAsyncFactory<T, TState> _factory;
    private readonly Queue<TaskCompletionSource<T>> _queue = new();
    private readonly LinkedList<T> _pool = new();
    private int _count;

    public AsyncPool(int count, IAsyncFactory<T, TState> factory)
    {
        _count = count;
        _factory = factory;
    }

    internal int QueueSize
    {
        get
        {
            lock (_lockObject)
            {
                return _queue.Count;
            }
        }
    }
    
    internal int PoolSize
    {
        get
        {
            lock (_lockObject)
            {
                return _pool.Count;
            }
        }
    }

    public async Task<IRent<T>> Rent(TState state, CancellationToken cancellationToken)
    {
        Task<T> valueTask;
        lock (_lockObject)
        {
            if (_pool.Count > 0)
            {
                valueTask = Task.FromResult(_pool.First!.Value);
                _pool.RemoveFirst();
            }
            else
            {
                if (_count > 0)
                {
                    valueTask = _factory.Create(state, cancellationToken);
                    _count--;
                }
                else
                {
                    var taskCompletionSource = new TaskCompletionSource<T>(cancellationToken);
                    valueTask = taskCompletionSource.Task;
                    _queue.Enqueue(taskCompletionSource);
                }
            }
        }

        var value = await valueTask;
        return new Rent<T>(value, () => { Release(value); });
    }
    
    private void Release(T value)
    {
        lock (_lockObject)
        {
            if (_queue.Count > 0)
            {
                var task = _queue.Dequeue();
                task.SetResult(value);       
            }
            else
            {
                _pool.AddLast(value);
            }
        }
    }
}