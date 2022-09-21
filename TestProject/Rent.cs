namespace TestProject;

public class Rent<T>: IRent<T>
{
    private readonly Action<T> _releaseAction;

    public Rent(T value, Action<T> releaseAction)
    {
        _releaseAction = releaseAction;
        Value = value;
    }
    
    public T Value { get; }

    public void Dispose() => _releaseAction(Value);
}