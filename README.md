# OrderSaga.NET

A production-quality example of the **Saga Pattern** in .NET 10 using **MassTransit** and **Azure Service Bus**.

Companion code for the article: [The Saga Pattern in .NET & Azure — An Architect's Deep Dive](https://asmashaikhtech.hashnode.dev)

---

## What's in this repo

| Project | Role |
|---|---|
| `OrderSaga.Contracts` | Shared message contracts (commands + events) |
| `OrderSaga.StateMachine` | MassTransit state machine + EF Core saga state + DbContext |
| `OrderSaga.Api` | HTTP API — accepts order submissions, starts the saga |
| `OrderSaga.InventoryWorker` | Handles `ReserveStock` and `ReleaseStock` (compensation) |
| `OrderSaga.PaymentWorker` | Handles `ProcessPayment` |
| `OrderSaga.ShippingWorker` | Handles `ShipOrder` |
| `OrderSaga.Tests` | xUnit + MassTransit in-memory harness — no Azure required |

## Architecture

```
POST /api/orders
       │
       ▼
  [OrderSubmitted]
       │
       ▼
  ┌─────────────────────┐
  │  OrderSagaStateMachine │  ← persisted in SQL Server
  └─────────────────────┘
       │
       ├─► ReserveStock ──► InventoryWorker
       │         └─► StockReserved / StockReservationFailed
       │
       ├─► ProcessPayment ──► PaymentWorker
       │         └─► PaymentProcessed / PaymentFailed
       │
       └─► ShipOrder ──► ShippingWorker
                 └─► OrderShipped

Compensating path (PaymentFailed):
  ReleaseStock ──► InventoryWorker
  CancelOrder  ──► (handled by Order Service / notifications)
```

## Prerequisites

- .NET 8 SDK
- SQL Server (or LocalDB for dev)
- Azure Service Bus namespace (Standard tier minimum for topics)

## Getting started

### 1. Clone

```bash
git clone https://github.com/YOUR_USERNAME/OrderSaga.NET.git
cd OrderSaga.NET
```

### 2. Configure

Copy and fill in connection strings:

```bash
cp OrderSaga.Api/appsettings.json OrderSaga.Api/appsettings.Development.json
```

Edit `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "SagaDb": "Server=localhost;Database=OrderSagaDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Azure": {
    "ServiceBus": {
      "ConnectionString": "Endpoint=sb://YOUR_NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=..."
    }
  }
}
```

Apply the same `Azure:ServiceBus:ConnectionString` to all three worker `appsettings.json` files.

### 3. Run migrations

```bash
cd OrderSaga.Api
dotnet ef database update
```

### 4. Run all services

Open 4 terminals:

```bash
# Terminal 1 — API
cd OrderSaga.Api && dotnet run

# Terminal 2 — Inventory
cd OrderSaga.InventoryWorker && dotnet run

# Terminal 3 — Payment
cd OrderSaga.PaymentWorker && dotnet run

# Terminal 4 — Shipping
cd OrderSaga.ShippingWorker && dotnet run
```

### 5. Submit an order

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "customer-123",
    "items": [
      { "productId": "prod-001", "quantity": 2, "unitPrice": 49.99 }
    ]
  }'
```

You'll get back an `orderId`. Watch the logs across all 4 terminals to see the saga execute step by step.

### 6. Run tests (no Azure required)

```bash
cd OrderSaga.Tests && dotnet test
```

Tests use MassTransit's in-memory test harness — no Azure Service Bus connection needed.

## Key production patterns demonstrated

| Pattern | Where |
|---|---|
| **State Machine Saga** | `OrderSaga.StateMachine/OrderSagaStateMachine.cs` |
| **Compensating transactions** | `PaymentFailed` handler → `ReleaseStock` + `CancelOrder` |
| **Idempotent consumers** | `ReserveStockConsumer`, `ReleaseStockConsumer`, `ProcessPaymentConsumer` |
| **Optimistic concurrency** | `ISagaVersion` on `OrderSagaState` + EF Core row version |
| **Outbox Pattern** | `UseInMemoryOutbox()` in `Program.cs` |
| **Retry with backoff** | `UseMessageRetry` on each receive endpoint |
| **OpenTelemetry** | `AddSource("MassTransit")` + Azure Monitor exporter |

## Bugs fixed from the article

The article code snippets are intentionally simplified for readability. This repo fixes the following production issues:

1. `UseMessageRetry` is on the **receive endpoint**, not bus-level (bus-level is silently ignored)
2. **All events** have explicit `Event(() => ..., x => x.CorrelateById(...))` correlation — not just the first
3. `OrderSagaState` implements `ISagaVersion` for optimistic concurrency
4. `UseInMemoryOutbox()` called with **no arguments** (v8 API — passing `ctx` causes a compile error)
5. EF Core column types explicitly defined to prevent migration issues

## License

MIT
