---
id: solid-srp-function
title: SOLID - Single Responsibility Principle (Function Level)
category: solid-principles
priority: critical
tags: [SOLID, SRP, single-responsibility, function-design]
related: [solid-srp-class, core-dry-extraction, core-kiss-simplicity]
---

# Single Responsibility Principle - Function Level

A method should do one thing at one level of abstraction. Long C# methods often mix validation, mapping, persistence, logging, and side effects in a way that makes behavior hard to reason about.

## Incorrect

```csharp
public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
{
    if (request.Items.Count == 0)
    {
        throw new InvalidOperationException("Order requires at least one item.");
    }

    var order = new Order(request.CustomerId);
    foreach (var item in request.Items)
    {
        order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
    }

    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync(cancellationToken);

    _logger.LogInformation("Created order {OrderId}", order.Id);
    await _publisher.PublishAsync(new OrderCreated(order.Id), cancellationToken);

    return new OrderDto(order.Id, order.Total);
}
```

## Correct

```csharp
public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
{
    Validate(request);

    var order = BuildOrder(request);
    await PersistAsync(order, cancellationToken);
    await PublishCreatedAsync(order, cancellationToken);

    return Map(order);
}

private static void Validate(CreateOrderRequest request)
{
    if (request.Items.Count == 0)
    {
        throw new InvalidOperationException("Order requires at least one item.");
    }
}

private static Order BuildOrder(CreateOrderRequest request)
{
    var order = new Order(request.CustomerId);
    foreach (var item in request.Items)
    {
        order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
    }

    return order;
}
```

## Benefits

- Each step becomes easy to read and test.
- Failures are easier to localize.
- Helper methods expose business intent.
- Refactoring can move seams into separate collaborators later.

## When to Apply

- Methods spanning validation, mutation, persistence, and integration calls
- Controller or handler methods that feel procedural and fragile
