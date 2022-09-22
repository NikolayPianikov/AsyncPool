namespace TestProject;

public interface IAsyncFactory<T, in TState>
{
    Task<T> CreateAsync(TState state, CancellationToken cancellationToken = default);
}