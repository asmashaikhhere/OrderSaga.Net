namespace OrderSaga.PaymentWorker;

public interface IPaymentService
{
    Task<bool> IsAlreadyChargedAsync(Guid orderId);
    Task<PaymentResult> ChargeAsync(Guid orderId, decimal amount);
}

public record PaymentResult(bool Success, string? FailureReason = null);

/// <summary>
/// In-memory implementation for demo purposes.
/// In production: integrate with Stripe / Adyen / Azure Payment APIs.
/// Always ensure charge is idempotent using your payment provider's
/// idempotency key feature (orderId is a natural idempotency key).
/// </summary>
public class InMemoryPaymentService : IPaymentService
{
    private readonly HashSet<Guid> _charged = new();
    private readonly Random _random = new();

    public Task<bool> IsAlreadyChargedAsync(Guid orderId)
        => Task.FromResult(_charged.Contains(orderId));

    public Task<PaymentResult> ChargeAsync(Guid orderId, decimal amount)
    {
        // Simulate 15% payment failure
        if (_random.Next(100) < 15)
            return Task.FromResult(
                new PaymentResult(false, "Card declined by issuer"));

        _charged.Add(orderId);
        return Task.FromResult(new PaymentResult(true));
    }
}
