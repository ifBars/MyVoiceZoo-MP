---
id: solid-isp-clients
title: SOLID - Interface Segregation (Client-Specific)
category: solid-principles
priority: critical
tags: [SOLID, ISP, interface-segregation, client-design]
related: [solid-isp-interfaces, solid-srp-class, solid-lsp-contracts]
---

# Interface Segregation Principle - Client-Specific Interfaces

Clients should depend only on the members they actually use. If a read-only workflow receives a full read-write dependency, the design is already wider than necessary.

## Incorrect

```csharp
public sealed class OrderSummaryJob
{
    private readonly IOrderGateway _orders;

    public OrderSummaryJob(IOrderGateway orders)
    {
        _orders = orders;
    }

    public async Task<IReadOnlyList<OrderSummary>> RunAsync(CancellationToken cancellationToken)
    {
        var recentOrders = await _orders.SearchAsync("pending", cancellationToken);
        return recentOrders.Select(x => new OrderSummary(x.Id, x.Total)).ToList();
    }
}
```

The job only reads, but it depends on save and delete behavior too.

## Correct

```csharp
public sealed class OrderSummaryJob
{
    private readonly IOrderReader _orders;

    public OrderSummaryJob(IOrderReader orders)
    {
        _orders = orders;
    }

    public async Task<IReadOnlyList<OrderSummary>> RunAsync(CancellationToken cancellationToken)
    {
        var recentOrders = await _orders.SearchAsync("pending", cancellationToken);
        return recentOrders.Select(x => new OrderSummary(x.Id, x.Total)).ToList();
    }
}
```

## Benefits

- Dependencies describe real usage.
- Read-only code cannot accidentally mutate state.
- Consumer tests become lighter.
- Interface evolution affects fewer callers.

## When to Apply

- Query jobs, projections, read models, exporters, and report builders
