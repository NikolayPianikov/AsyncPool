namespace TestProject;

public class Rent<T>: IRent<T>
{
    private readonly Action _releaseAction;

    public Rent(T value, Action releaseAction)
    {
        _releaseAction = releaseAction;
        Value = value;
    }
    
    public T Value { get; }

    public void Dispose() => _releaseAction();
}