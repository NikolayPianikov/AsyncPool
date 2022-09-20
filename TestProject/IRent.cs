namespace TestProject;

public interface IRent<out T>: IDisposable
{
    T Value { get; }
}