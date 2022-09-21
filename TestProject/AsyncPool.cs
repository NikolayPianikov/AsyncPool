namespace TestProject;

public class AsyncPool<T, TState> : IAsyncPool<T, TState>, IPoolManager
{
    private readonly object _lockObject = new();
    private readonly IAsyncFactory<T, TState> _factory;
    private readonly LinkedList<Source> _queue = new();
    private readonly LinkedList<T> _pool = new();
    private readonly int _size;
    private int _currentSize;

    public AsyncPool(int size, IAsyncFactory<T, TState> factory)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        _size = size;
        _currentSize = size;
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

    public async Task<IRent<T>> RentAsync(TState state, CancellationToken cancellationToken)
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
                if (_currentSize > 0)
                {
                    valueTask = _factory.CreateAsync(state, cancellationToken);
                    _currentSize--;
                }
                else
                {
                    var taskSource = new TaskCompletionSource<T>(cancellationToken);
                    valueTask = taskSource.Task;
                    var sources = new Source?[1];
                    var registration = cancellationToken.Register(() =>
                    {
                        lock (_lockObject)
                        {
                            taskSource.TrySetCanceled();
                            var source = sources[0];
                            if (source.HasValue)
                            {
                                _queue.Remove(source.Value);
                            }
                        }
                    });

                    var source = new Source(taskSource, registration);
                    sources[0] = source;
                    _queue.AddLast(source);
                }
            }
        }

        return new Rent<T>(await valueTask, Release);
    }

    public void Clear()
    {
        lock (_lockObject)
        {
            _pool.Clear();
            foreach (var source in _queue)
            {
                source.TaskSource.TrySetCanceled();
                source.CancellationRegistration.Dispose();
            }

            _queue.Clear();
            _currentSize = _size;
        }
    }

    private void Release(T value)
    {
        lock (_lockObject)
        {
            while(_queue.Count > 0)
            {
                var source = _queue.First!.Value;
                _queue.RemoveFirst();
                source.CancellationRegistration.Dispose();
                if (source.TaskSource.TrySetResult(value))
                {
                    return;
                }
            }
            
            _pool.AddLast(value);
        }
    }

    private readonly record struct Source(TaskCompletionSource<T> TaskSource, IDisposable CancellationRegistration);
}