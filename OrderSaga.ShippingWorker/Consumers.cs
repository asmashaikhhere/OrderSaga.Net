using MassTransit;
using OrderSaga.Contracts;

namespace OrderSaga.ShippingWorker;

/// <summary>
/// Handles shipping — the final step of the happy path.
/// Publishes OrderShipped with a tracking number on success.
/// </summary>
public class ShipOrderConsumer : IConsumer<ShipOrder>
{
    private readonly ILogger<ShipOrderConsumer> _logger;

    public ShipOrderConsumer(ILogger<ShipOrderConsumer> logger)
        => _logger = logger;

    public async Task Consume(ConsumeContext<ShipOrder> context)
    {
        var orderId = context.Message.OrderId;
        _logger.LogInformation("Shipping order OrderId={OrderId}", orderId);

        // In production: call courier API, generate label, persist shipment record
        var trackingNumber = $"TRACK-{orderId.ToString("N")[..8].ToUpper()}";

        await Task.Delay(100); // Simulate courier API call

        _logger.LogInformation(
            "Order shipped OrderId={OrderId} TrackingNumber={TrackingNumber}",
            orderId, trackingNumber);

        await context.Publish(new OrderShipped(orderId, trackingNumber));
    }
}
