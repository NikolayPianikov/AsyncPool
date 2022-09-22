namespace TestProject;

public class AsyncPool<T, TState> : IAsyncPool<T, TState>
{
    private readonly IAsyncFactory<T, TState> _factory;
    private readonly SemaphoreSlim _semaphore;
    private readonly Queue<T> _pool = new();

    public AsyncPool(SemaphoreSlim semaphore, IAsyncFactory<T, TState> factory)
    {
        _semaphore = semaphore;
        _factory = factory;
    }
    
    internal int PoolSize
    {
        get
        {
            lock (_pool)
            {
                return _pool.Count;
            }
        }
    }

    public async Task<IRent<T>> RentAsync(TState state, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        lock (_pool)
        {
            if (_pool.Count > 0)
            {
                return new Rent<T>(_pool.Dequeue(), Release);
            }
        }
        
        return new Rent<T>(await _factory.CreateAsync(state, cancellationToken), Release);
    }
    
    private void Release(T value)
    {
        lock (_pool)
        {
            _pool.Enqueue(value);
        }
        
        _semaphore.Release(1);
    }
}