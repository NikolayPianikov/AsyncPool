namespace TestProject;

public interface IAsyncPool<T, in TState>
{
    Task<IRent<T>> Rent(TState state, CancellationToken cancellationToken);
}