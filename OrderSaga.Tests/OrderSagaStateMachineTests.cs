using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderSaga.Contracts;
using OrderSaga.StateMachine;

namespace OrderSaga.Tests;

public class OrderSagaStateMachineTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<OrderSagaStateMachine, OrderSagaState>();
            })
            .AddSingleton(NullLogger<OrderSagaStateMachine>.Instance)
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        await Task.Delay(500);
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task HappyPath_OrderCompletes_WhenStockAndPaymentSucceed()
    {
        var orderId = Guid.NewGuid();

        await _harness.Bus.Publish(new OrderSubmitted(
            orderId, "customer-1", 99.99m,
            [new OrderItem("prod-1", 2, 49.99m)]));

        await Task.Delay(500);

        (await _harness.Published.Any<ReserveStock>())
            .Should().BeTrue("saga must send ReserveStock after OrderSubmitted");

        await _harness.Bus.Publish(new StockReserved(orderId));
        await Task.Delay(500);

        (await _harness.Published.Any<ProcessPayment>())
            .Should().BeTrue("saga must send ProcessPayment after StockReserved");

        await _harness.Bus.Publish(new PaymentProcessed(orderId));
        await Task.Delay(500);

        (await _harness.Published.Any<ShipOrder>())
            .Should().BeTrue("saga must publish ShipOrder after PaymentProcessed");
    }

    [Fact]
    public async Task StockFailure_CompensatesCorrectly_WithCancelOrder()
    {
        var orderId = Guid.NewGuid();

        await _harness.Bus.Publish(new OrderSubmitted(
            orderId, "customer-2", 150m,
            [new OrderItem("prod-out-of-stock", 1, 150m)]));

        await Task.Delay(500);

        (await _harness.Published.Any<ReserveStock>())
            .Should().BeTrue();

        await _harness.Bus.Publish(
            new StockReservationFailed(orderId, "Out of stock"));

        await Task.Delay(500);

        (await _harness.Published.Any<ProcessPayment>())
            .Should().BeFalse("payment must NOT be requested when stock fails");

        (await _harness.Published.Any<CancelOrder>())
            .Should().BeTrue("CancelOrder compensation must be published");
    }

    [Fact]
    public async Task PaymentFailure_CompensatesCorrectly_WithReleaseStockAndCancel()
    {
        var orderId = Guid.NewGuid();

        await _harness.Bus.Publish(new OrderSubmitted(
            orderId, "customer-3", 200m,
            [new OrderItem("prod-2", 1, 200m)]));

        await Task.Delay(500);

        await _harness.Bus.Publish(new StockReserved(orderId));
        await Task.Delay(500);

        await _harness.Bus.Publish(
            new PaymentFailed(orderId, "Card declined"));

        await Task.Delay(500);

        (await _harness.Published.Any<ReleaseStock>())
            .Should().BeTrue("ReleaseStock compensation must be published");

        (await _harness.Published.Any<CancelOrder>())
            .Should().BeTrue("CancelOrder compensation must be published");
    }

    [Fact]
    public async Task DuplicateOrderSubmitted_IsIdempotent_OnlyOneReserveStockPublished()
    {
        var orderId = Guid.NewGuid();
        var order = new OrderSubmitted(
            orderId, "customer-4", 50m,
            [new OrderItem("prod-3", 1, 50m)]);

        await _harness.Bus.Publish(order);
        await _harness.Bus.Publish(order);

        await Task.Delay(1000);

        var reserveCount = _harness.Published
            .Select<ReserveStock>()
            .Count(m => m.Context.Message.OrderId == orderId);

        reserveCount.Should().Be(1,
            "duplicate OrderSubmitted must not result in duplicate ReserveStock");
    }
}