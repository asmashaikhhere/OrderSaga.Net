using MassTransit;
using Microsoft.Extensions.Logging;
using OrderSaga.Contracts;

namespace OrderSaga.StateMachine;

/// <summary>
/// Orchestrated Saga using MassTransit state machine.
/// Owns the full lifecycle of an order across Inventory, Payment, and Shipping.
///
/// Production notes:
/// - Every event has explicit correlation defined (required by MassTransit v8)
/// - ISagaVersion on state enables optimistic concurrency via EF Core
/// - SetCompletedWhenFinalized() removes saga row on success (keeps table lean)
/// - UpdatedAt is stamped on every transition via Then()
/// </summary>
public class OrderSagaStateMachine : MassTransitStateMachine<OrderSagaState>
{
    private readonly ILogger<OrderSagaStateMachine> _logger;

    // ---------- States ----------
    public State StockPending { get; private set; } = null!;
    public State PaymentPending { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    // ---------- Events ----------
    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<StockReserved> StockReserved { get; private set; } = null!;
    public Event<StockReservationFailed> StockFailed { get; private set; } = null!;
    public Event<PaymentProcessed> PaymentProcessed { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;

    public OrderSagaStateMachine(ILogger<OrderSagaStateMachine> logger)
    {
        _logger = logger;

        // Map CurrentState property to the state machine state
        InstanceState(x => x.CurrentState);

        // ----------------------------------------------------------------
        // CRITICAL: Every event must have correlation configured.
        // Missing this means MassTransit cannot match incoming messages
        // to the correct saga instance — they get silently discarded.
        // ----------------------------------------------------------------
        Event(() => OrderSubmitted,
            x => x.CorrelateById(m => m.Message.OrderId));

        Event(() => StockReserved,
            x => x.CorrelateById(m => m.Message.OrderId));

        Event(() => StockFailed,
            x => x.CorrelateById(m => m.Message.OrderId));

        Event(() => PaymentProcessed,
            x => x.CorrelateById(m => m.Message.OrderId));

        Event(() => PaymentFailed,
            x => x.CorrelateById(m => m.Message.OrderId));

        // ----------------------------------------------------------------
        // Initial state — saga starts here when OrderSubmitted arrives
        // ----------------------------------------------------------------
        Initially(
            When(OrderSubmitted)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId = ctx.Message.OrderId;
                    ctx.Saga.CustomerId = ctx.Message.CustomerId;
                    ctx.Saga.TotalAmount = ctx.Message.TotalAmount;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Saga started for OrderId={OrderId} CustomerId={CustomerId}",
                        ctx.Saga.OrderId, ctx.Saga.CustomerId);
                })
                .Publish(ctx => new ReserveStock(
                    ctx.Saga.OrderId,
                    ctx.Message.Items))
                .TransitionTo(StockPending));

        // ----------------------------------------------------------------
        // StockPending — waiting for inventory response
        // ----------------------------------------------------------------
        During(StockPending,
            When(StockReserved)
                .Then(ctx =>
                {
                    ctx.Saga.StockReserved = true;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Stock reserved for OrderId={OrderId}", ctx.Saga.OrderId);
                })
                .Publish(ctx => new ProcessPayment(
                    ctx.Saga.OrderId,
                    ctx.Saga.TotalAmount))
                .TransitionTo(PaymentPending),

            When(StockFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;

                    _logger.LogWarning(
                        "Stock reservation failed for OrderId={OrderId} Reason={Reason}",
                        ctx.Saga.OrderId, ctx.Message.Reason);
                })
                // No stock was touched — just cancel the order
                .Publish(ctx => new CancelOrder(
                    ctx.Saga.OrderId,
                    ctx.Saga.FailureReason ?? "Stock unavailable"))
                .TransitionTo(Failed));

        // ----------------------------------------------------------------
        // PaymentPending — waiting for payment response
        // ----------------------------------------------------------------
        During(PaymentPending,
            When(PaymentProcessed)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentProcessed = true;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation(
                        "Payment processed for OrderId={OrderId}", ctx.Saga.OrderId);
                })
                .Publish(ctx => new ShipOrder(ctx.Saga.OrderId))
                .TransitionTo(Completed)
                .Finalize(), // Removes row from DB — saga is done

            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAt = DateTime.UtcNow;

                    _logger.LogWarning(
                        "Payment failed for OrderId={OrderId} Reason={Reason}",
                        ctx.Saga.OrderId, ctx.Message.Reason);
                })
                // Compensating transactions — reverse the stock reservation
                .Publish(ctx => new ReleaseStock(ctx.Saga.OrderId))
                .Publish(ctx => new CancelOrder(
                    ctx.Saga.OrderId,
                    ctx.Message.Reason))
                .TransitionTo(Failed));

        // Removes the saga row from DB when Finalize() is called
        SetCompletedWhenFinalized();
    }
}
