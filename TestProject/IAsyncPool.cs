namespace TestProject;

public interface IAsyncPool<T, in TState>
{
    Task<IRent<T>> RentAsync(TState state, CancellationToken cancellationToken = default);
}