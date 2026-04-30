using System.ComponentModel.DataAnnotations.Schema;
using MassTransit;

namespace OrderSaga.StateMachine;

/// <summary>
/// Persisted state for each in-flight order saga instance.
/// Keyed by CorrelationId which maps to OrderId.
/// EF Core persists this between steps — survives restarts.
/// </summary>
[Table("OrderSagaState")]
public class OrderSagaState : SagaStateMachineInstance, ISagaVersion
{
    // Required by MassTransit — maps to OrderId (our correlation key)
    public Guid CorrelationId { get; set; }

    // Required by MassTransit for optimistic concurrency
    public int Version { get; set; }

    // Current state name — persisted as a string
    public string CurrentState { get; set; } = null!;

    // Business data carried through the saga
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = null!;
    public decimal TotalAmount { get; set; }

    // Step completion flags — used for idempotency checks
    public bool StockReserved { get; set; }
    public bool PaymentProcessed { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Failure tracking
    public string? FailureReason { get; set; }
}
