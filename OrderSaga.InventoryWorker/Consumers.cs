using MassTransit;
using OrderSaga.Contracts;

namespace OrderSaga.InventoryWorker;

/// <summary>
/// Handles stock reservation — the first step of the saga.
/// Simulates inventory check; in production replace with real DB logic.
/// </summary>
public class ReserveStockConsumer : IConsumer<ReserveStock>
{
    private readonly IInventoryService _inventory;
    private readonly ILogger<ReserveStockConsumer> _logger;

    public ReserveStockConsumer(
        IInventoryService inventory,
        ILogger<ReserveStockConsumer> logger)
    {
        _inventory = inventory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReserveStock> context)
    {
        var orderId = context.Message.OrderId;
        _logger.LogInformation("Reserving stock for OrderId={OrderId}", orderId);

        // Idempotency: if already reserved, just re-publish success
        if (await _inventory.IsAlreadyReservedAsync(orderId))
        {
            _logger.LogInformation(
                "Stock already reserved for OrderId={OrderId} — republishing event",
                orderId);
            await context.Publish(new StockReserved(orderId));
            return;
        }

        var success = await _inventory.TryReserveAsync(
            orderId, context.Message.Items);

        if (success)
        {
            _logger.LogInformation(
                "Stock reserved successfully for OrderId={OrderId}", orderId);
            await context.Publish(new StockReserved(orderId));
        }
        else
        {
            _logger.LogWarning(
                "Stock reservation failed for OrderId={OrderId}", orderId);
            await context.Publish(new StockReservationFailed(
                orderId,
                "One or more items are out of stock"));
        }
    }
}

/// <summary>
/// Compensating consumer — releases stock when payment fails.
/// Must be idempotent: if stock is already released, do nothing.
/// </summary>
public class ReleaseStockConsumer : IConsumer<ReleaseStock>
{
    private readonly IInventoryService _inventory;
    private readonly ILogger<ReleaseStockConsumer> _logger;

    public ReleaseStockConsumer(
        IInventoryService inventory,
        ILogger<ReleaseStockConsumer> logger)
    {
        _inventory = inventory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReleaseStock> context)
    {
        var orderId = context.Message.OrderId;
        _logger.LogInformation("Releasing stock for OrderId={OrderId}", orderId);

        // Idempotency guard — critical for compensating transactions
        if (!await _inventory.IsAlreadyReservedAsync(orderId))
        {
            _logger.LogInformation(
                "Stock for OrderId={OrderId} already released — skipping",
                orderId);
            return;
        }

        await _inventory.ReleaseAsync(orderId);
        _logger.LogInformation(
            "Stock released for OrderId={OrderId}", orderId);
    }
}
