namespace TestProject;

public interface IAsyncFactory<T, in TState>
{
    Task<T> Create(TState state, CancellationToken cancellationToken);
}