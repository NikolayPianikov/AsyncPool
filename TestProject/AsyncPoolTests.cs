using Moq;
using Shouldly;
using Range = Moq.Range;

namespace TestProject;

public class AsyncPoolTests: IDisposable
{
    private readonly Mock<IAsyncFactory<int, int>> _factory = new();
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;

    public AsyncPoolTests()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        _factory
            .Setup(i => i.CreateAsync(It.IsAny<int>(), _cancellationToken))
            .Returns<int, CancellationToken>((state, _) => Task.FromResult(state + 1));
    }
    
    [Fact]
    public async Task ShouldUseFactory()
    {
        // Given
        var pool = CreateInstance(1);
        
        // When
        var rent = await pool.RentAsync(1, _cancellationToken);

        // Then
        rent.Value.ShouldBe(2);
        pool.PoolSize.ShouldBe(0);
        _factory.Verify(i => i.CreateAsync(1, _cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ShouldReturnValueToPoolWhenDispose()
    {
        // Given
        var pool = CreateInstance(1);
        var rent = await pool.RentAsync(1, _cancellationToken);
        
        // When
        rent.Dispose();

        // Then
        pool.PoolSize.ShouldBe(1);
    }

    [Fact]
    public async Task ShouldReuseValueFromPool()
    {
        // Given
        var pool = CreateInstance(1);
        var rent = await pool.RentAsync(1, _cancellationToken);
        
        // When
        rent.Dispose();
        rent = await pool.RentAsync(99, _cancellationToken);

        // Then
        rent.Value.ShouldBe(2);
        _factory.Verify(i => i.CreateAsync(1, _cancellationToken), Times.Once);
    }
    
    [Fact]
    public async Task ShouldWaitForRelease()
    {
        // Given
        var pool = CreateInstance(1);
        var rent = await pool.RentAsync(1, _cancellationToken);
        
        // When
        var rentTask = pool.RentAsync(99, _cancellationToken);
        pool.PoolSize.ShouldBe(0);
        rent.Dispose();

        // Then
        rentTask.Result.Value.ShouldBe(2);
        pool.PoolSize.ShouldBe(0);
        _factory.Verify(i => i.CreateAsync(1, _cancellationToken), Times.Once);
    }
    
    [Fact]
    public async Task ShouldNotProvideValueForRentWhenTaskIsCanceled()
    {
        // Given
        var pool = CreateInstance(1);
        await pool.RentAsync(1, _cancellationToken);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // When
        var rentTask = pool.RentAsync(99, cancellationTokenSource.Token);
        new SpinWait().SpinOnce();
        cancellationTokenSource.Cancel();

        // Then
        rentTask.Status.ShouldNotBe(TaskStatus.Canceled);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    public void ShouldNotExceedSize(int size)
    {
        // Given
        var pool = CreateInstance(size);
        var tasks = new List<Task>();
        var spinWait = new SpinWait();
        
        // When
        for (var i = 0; i < size * 10; i++)
        {
            var task = pool
                .RentAsync(1, _cancellationToken)
                .ContinueWith(task =>
                    {
                        spinWait.SpinOnce();
                        task.Result.Dispose();
                    },
                    _cancellationToken);
                
            tasks.Add(task);    
        }

        Task.WaitAll(tasks.ToArray());
        
        // Then
        pool.PoolSize.ShouldBeLessThanOrEqualTo(size);
        _factory.Verify(i => i.CreateAsync(1, _cancellationToken), Times.Between(1, size, Range.Inclusive));
    }

    private AsyncPool<int, int> CreateInstance(int size) => new(new SemaphoreSlim(size), _factory.Object);

    void IDisposable.Dispose() => _cancellationTokenSource.Dispose();
}