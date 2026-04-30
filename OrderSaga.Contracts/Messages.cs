namespace OrderSaga.Contracts;

// ---------------------------------------------------------------------------
// Commands — sent TO a service (imperative: "do this")
// ---------------------------------------------------------------------------

public record ReserveStock(Guid OrderId, List<OrderItem> Items);

public record ReleaseStock(Guid OrderId);

public record ProcessPayment(Guid OrderId, decimal Amount);

public record ShipOrder(Guid OrderId);

public record CancelOrder(Guid OrderId, string Reason);

// ---------------------------------------------------------------------------
// Events — published BY a service (declarative: "this happened")
// ---------------------------------------------------------------------------

public record OrderSubmitted(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    List<OrderItem> Items);

public record StockReserved(Guid OrderId);

public record StockReservationFailed(Guid OrderId, string Reason);

public record PaymentProcessed(Guid OrderId);

public record PaymentFailed(Guid OrderId, string Reason);

public record OrderShipped(Guid OrderId, string TrackingNumber);

public record OrderCancelled(Guid OrderId, string Reason);

// ---------------------------------------------------------------------------
// Shared value objects
// ---------------------------------------------------------------------------

public record OrderItem(string ProductId, int Quantity, decimal UnitPrice);
