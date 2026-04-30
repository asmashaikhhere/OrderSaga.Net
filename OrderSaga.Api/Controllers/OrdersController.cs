using MassTransit;
using Microsoft.AspNetCore.Mvc;
using OrderSaga.Contracts;

namespace OrderSaga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IPublishEndpoint publishEndpoint,
        ILogger<OrdersController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Submits a new order — publishes OrderSubmitted which starts the saga.
    /// The saga CorrelationId is the OrderId, so clients can track status.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitOrder(
        [FromBody] SubmitOrderRequest request,
        CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();

        _logger.LogInformation(
            "Submitting order OrderId={OrderId} CustomerId={CustomerId}",
            orderId, request.CustomerId);

        await _publishEndpoint.Publish(new OrderSubmitted(
            orderId,
            request.CustomerId,
            request.Items.Sum(i => i.Quantity * i.UnitPrice),
            request.Items.Select(i =>
                new OrderItem(i.ProductId, i.Quantity, i.UnitPrice)).ToList()),
            cancellationToken);

        return Accepted(new { OrderId = orderId });
    }
}

public record SubmitOrderRequest(
    string CustomerId,
    List<SubmitOrderItem> Items);

public record SubmitOrderItem(
    string ProductId,
    int Quantity,
    decimal UnitPrice);
