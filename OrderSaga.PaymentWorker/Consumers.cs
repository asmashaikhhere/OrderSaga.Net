using MassTransit;
using OrderSaga.Contracts;

namespace OrderSaga.PaymentWorker;

/// <summary>
/// Handles payment processing — the second step of the saga.
/// Simulates 15% payment failure for demo purposes.
/// </summary>
public class ProcessPaymentConsumer : IConsumer<ProcessPayment>
{
    private readonly IPaymentService _payment;
    private readonly ILogger<ProcessPaymentConsumer> _logger;

    public ProcessPaymentConsumer(
        IPaymentService payment,
        ILogger<ProcessPaymentConsumer> logger)
    {
        _payment = payment;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        var orderId = context.Message.OrderId;
        _logger.LogInformation(
            "Processing payment for OrderId={OrderId} Amount={Amount}",
            orderId, context.Message.Amount);

        // Idempotency: already charged? Re-publish success event.
        if (await _payment.IsAlreadyChargedAsync(orderId))
        {
            _logger.LogInformation(
                "Payment already processed for OrderId={OrderId}", orderId);
            await context.Publish(new PaymentProcessed(orderId));
            return;
        }

        var result = await _payment.ChargeAsync(orderId, context.Message.Amount);

        if (result.Success)
        {
            _logger.LogInformation(
                "Payment succeeded for OrderId={OrderId}", orderId);
            await context.Publish(new PaymentProcessed(orderId));
        }
        else
        {
            _logger.LogWarning(
                "Payment failed for OrderId={OrderId} Reason={Reason}",
                orderId, result.FailureReason);
            await context.Publish(new PaymentFailed(orderId, result.FailureReason!));
        }
    }
}
