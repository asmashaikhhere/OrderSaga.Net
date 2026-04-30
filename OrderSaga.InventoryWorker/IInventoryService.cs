using OrderSaga.Contracts;

namespace OrderSaga.InventoryWorker;

public interface IInventoryService
{
    Task<bool> IsAlreadyReservedAsync(Guid orderId);
    Task<bool> TryReserveAsync(Guid orderId, List<OrderItem> items);
    Task ReleaseAsync(Guid orderId);
}

/// <summary>
/// In-memory implementation for demo purposes.
/// Replace with real EF Core / SQL implementation in production.
/// Key production requirements:
/// - Use a dedicated Reservations table with OrderId as unique key
/// - Wrap reserve + insert in a single transaction
/// - Use SELECT FOR UPDATE or optimistic concurrency on release
/// </summary>
public class InMemoryInventoryService : IInventoryService
{
    private readonly HashSet<Guid> _reserved = new();
    private readonly ILogger<InMemoryInventoryService> _logger;

    // Simulates 10% stock failure for demo purposes
    private readonly Random _random = new();

    public InMemoryInventoryService(ILogger<InMemoryInventoryService> logger)
        => _logger = logger;

    public Task<bool> IsAlreadyReservedAsync(Guid orderId)
        => Task.FromResult(_reserved.Contains(orderId));

    public Task<bool> TryReserveAsync(Guid orderId, List<OrderItem> items)
    {
        // Simulate occasional stock failure
        if (_random.Next(10) == 0)
            return Task.FromResult(false);

        _reserved.Add(orderId);
        return Task.FromResult(true);
    }

    public Task ReleaseAsync(Guid orderId)
    {
        _reserved.Remove(orderId);
        return Task.CompletedTask;
    }
}
