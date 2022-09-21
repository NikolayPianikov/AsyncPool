using Moq;
using Shouldly;

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
        pool.QueueSize.ShouldBe(1);
        pool.PoolSize.ShouldBe(0);
        rent.Dispose();
        pool.QueueSize.ShouldBe(0);
        rent.Dispose();

        // Then
        rentTask.Result.Value.ShouldBe(2);
        pool.PoolSize.ShouldBe(1);
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
        cancellationTokenSource.Cancel();
        rentTask.Status.ShouldBe(TaskStatus.Canceled);

        // Then
        rentTask.Status.ShouldBe(TaskStatus.Canceled);
    }
    
    [Fact]
    public async Task ShouldClearQueueWhenTaskIsCanceled()
    {
        // Given
        var pool = CreateInstance(1);
        await pool.RentAsync(1, _cancellationToken);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // When
        var rentTask = pool.RentAsync(99, cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();
        rentTask.Status.ShouldBe(TaskStatus.Canceled);

        // Then
        pool.QueueSize.ShouldBe(0);
    }
    
    [Fact]
    public async Task ShouldClearAndCancelTasks()
    {
        // Given
        var pool = CreateInstance(2);
        await pool.RentAsync(1, _cancellationToken);
        await pool.RentAsync(99, _cancellationToken);
        var rentTask1 = pool.RentAsync(99, _cancellationToken);
        var rentTask2 = pool.RentAsync(99, _cancellationToken);
        pool.QueueSize.ShouldBe(2);
        pool.PoolSize.ShouldBe(0);
        
        // When
        pool.Clear();

        // Then
        pool.PoolSize.ShouldBe(0);
        pool.QueueSize.ShouldBe(0);
        rentTask1.Status.ShouldBe(TaskStatus.Canceled);
        rentTask2.Status.ShouldBe(TaskStatus.Canceled);
    }
    
    [Fact]
    public async Task ShouldRentAfterClear()
    {
        // Given
        var pool = CreateInstance(2);
        await pool.RentAsync(1, _cancellationToken);
        await pool.RentAsync(99, _cancellationToken);
#pragma warning disable CS4014
        pool.RentAsync(99, _cancellationToken);
        pool.RentAsync(99, _cancellationToken);
#pragma warning restore CS4014
        pool.Clear();
        
        // When
        var rent = await pool.RentAsync(33, _cancellationToken);

        // Then
        rent.Value.ShouldBe(34);
    }
    
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ShouldThrowArgumentOutOfRangeExceptionWhenSizeIsEqOrLessZero(int size)
    {
        // Given

        // When
        
        // Then
        Should.Throw<ArgumentOutOfRangeException>(() => CreateInstance(size));
    }
    
    private AsyncPool<int, int> CreateInstance(int size) => new(size, _factory.Object);

    void IDisposable.Dispose() => _cancellationTokenSource.Dispose();
}