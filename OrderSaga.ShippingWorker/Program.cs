using MassTransit;
using OrderSaga.ShippingWorker;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ShipOrderConsumer>();

    // PRODUCTION — Azure Service Bus
    //x.UsingAzureServiceBus((ctx, cfg) =>
    //{
    //    cfg.Host(config["Azure:ServiceBus:ConnectionString"]);
    //    cfg.ReceiveEndpoint("order-saga", e =>
    //    {
    //        e.UseMessageRetry(r =>
    //            r.Exponential(5,
    //                TimeSpan.FromSeconds(1),
    //                TimeSpan.FromSeconds(30),
    //                TimeSpan.FromSeconds(5)));
    //        e.UseInMemoryOutbox(ctx);
    //        e.ConfigureSaga<OrderSagaState>(ctx);
    //    });
    //});

    // LOCAL DEV — RabbitMQ
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("shipping-ship", e =>
        {
            e.UseMessageRetry(r =>
                r.Exponential(5,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(5)));
            e.ConfigureConsumer<ShipOrderConsumer>(ctx);
        });
    });
});

var host = builder.Build();
host.Run();
