using Moq;
using Shouldly;

namespace TestProject;

public class UnitTest1: IDisposable
{
    private readonly Mock<IAsyncFactory<int, int>> _factory = new();
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;

    public UnitTest1()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        _factory
            .Setup(i => i.Create(It.IsAny<int>(), _cancellationToken))
            .Returns<int, CancellationToken>((state, cancellationToken) => Task.FromResult(state + 1));
    }
    
    [Fact]
    public async Task ShouldUseFactory()
    {
        // Given
        var pool = new AsyncPool<int, int>(1, _factory.Object);
        
        // When
        var rent = await pool.Rent(1, _cancellationToken);

        // Then
        rent.Value.ShouldBe(2);
        pool.PoolSize.ShouldBe(0);
        _factory.Verify(i => i.Create(1, _cancellationToken), Times.Once);
    }
    
    [Fact]
    public async Task ShouldReturnValueToPoolWhenDispose()
    {
        // Given
        var pool = new AsyncPool<int, int>(1, _factory.Object);
        var rent = await pool.Rent(1, _cancellationToken);
        
        // When
        rent.Dispose();

        // Then
        pool.PoolSize.ShouldBe(1);
    }
    
    [Fact]
    public async Task ShouldReuseValueFromPool()
    {
        // Given
        var pool = new AsyncPool<int, int>(1, _factory.Object);
        var rent = await pool.Rent(1, _cancellationToken);
        
        // When
        rent.Dispose();
        rent = await pool.Rent(99, _cancellationToken);

        // Then
        rent.Value.ShouldBe(2);
        _factory.Verify(i => i.Create(1, _cancellationToken), Times.Once);
    }
    
    [Fact]
    public async Task ShouldWaitForRelease()
    {
        // Given
        var pool = new AsyncPool<int, int>(1, _factory.Object);
        var rent = await pool.Rent(1, _cancellationToken);
        
        // When
        var rentTask = pool.Rent(99, _cancellationToken);
        pool.QueueSize.ShouldBe(1);
        pool.PoolSize.ShouldBe(0);
        rent.Dispose();
        pool.QueueSize.ShouldBe(0);
        rent.Dispose();

        // Then
        rentTask.Result.Value.ShouldBe(2);
        pool.PoolSize.ShouldBe(1);
        _factory.Verify(i => i.Create(1, _cancellationToken), Times.Once);
    }

    public void Dispose() => _cancellationTokenSource.Dispose();
}