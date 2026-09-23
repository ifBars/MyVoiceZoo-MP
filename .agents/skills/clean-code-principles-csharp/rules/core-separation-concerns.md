---
id: core-separation-concerns
title: Separation of Concerns
category: core-principles
priority: critical
tags: [separation-of-concerns, modularity, cohesion]
related: [solid-srp-class, solid-srp-function, core-law-demeter]
---

# Separation of Concerns

Keep presentation, application orchestration, domain logic, persistence, and integration concerns in their own places. In C#, problems often start when controllers, handlers, or `MonoBehaviour` classes take on everything.

## Incorrect

```csharp
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var order = new Order(request.CustomerId);
        foreach (var item in request.Items)
        {
            order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        await _emailSender.SendAsync(order.CustomerId.ToString(), "Order created");

        return Ok(order.Id);
    }
}
```

## Correct

```csharp
public sealed class OrdersController : ControllerBase
{
    private readonly OrderApplicationService _orders;

    public OrdersController(OrderApplicationService orders)
    {
        _orders = orders;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var orderId = await _orders.CreateAsync(request, cancellationToken);
        return Ok(orderId);
    }
}
```

## Benefits

- Layers stay easier to reason about.
- Testing scope becomes clear.
- Infrastructure changes do not leak everywhere.
- UI or transport code stays thin.

## When to Apply

- Controllers and endpoints
- Unity behaviors coordinating gameplay plus persistence plus UI
- Builders that start taking on domain orchestration
