using Azure.Monitor.OpenTelemetry.AspNetCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using OrderSaga.StateMachine;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// -----------------------------------------------------------------------
// EF Core — saga state persistence
// -----------------------------------------------------------------------
builder.Services.AddDbContext<SagaDbContext>(opts =>
    opts.UseSqlServer(
        config.GetConnectionString("SagaDb"),
        sql => sql.MigrationsAssembly("OrderSaga.Api")));

// -----------------------------------------------------------------------
// MassTransit — state machine + Azure Service Bus
//
// Production notes:
// - UseMessageRetry goes on the receive endpoint, NOT on the bus level.
//   At bus level it is silently ignored for Azure Service Bus transport.
// - UseInMemoryOutbox() (no args in v8) guarantees messages published
//   inside a consumer are only dispatched after the consumer completes
//   successfully — prevents ghost messages on consumer failure.
// - ConcurrencyMode.Optimistic uses ISagaVersion (row version) to
//   prevent two concurrent messages overwriting each other's state.
// -----------------------------------------------------------------------
builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderSagaStateMachine, OrderSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<SagaDbContext>();
        });

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
            cfg.ReceiveEndpoint("order-saga", e =>
            {
                e.UseMessageRetry(r =>
                    r.Exponential(5,
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromSeconds(5)));
                e.UseInMemoryOutbox(ctx);
                e.ConfigureSaga<OrderSagaState>(ctx);
            });
        });
});

// -----------------------------------------------------------------------
// OpenTelemetry — distributed tracing across all saga services
// -----------------------------------------------------------------------
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("MassTransit")
        .AddAspNetCoreInstrumentation());
       // .AddEntityFrameworkCoreInstrumentation());

if (!string.IsNullOrWhiteSpace(config["AzureMonitor:ConnectionString"]))
{
    builder.Services.AddOpenTelemetry()
        .UseAzureMonitor(o =>
            o.ConnectionString = config["AzureMonitor:ConnectionString"]);
}

builder.Services.AddControllers();

var app = builder.Build();

// Apply EF Core migrations on startup (dev/staging only — use pipeline in prod)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();

app.Run();
