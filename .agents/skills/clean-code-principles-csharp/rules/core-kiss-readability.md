---
id: core-kiss-readability
title: KISS - Readability
category: core-principles
priority: critical
tags: [KISS, readability, clear-code, maintainability]
related: [core-kiss-simplicity, solid-srp-function]
---

# KISS - Readability

Readable code usually beats clever code. In C#, dense LINQ, nested ternaries, and hidden side effects can make logic harder to understand than a direct loop or a small helper method.

## Incorrect

```csharp
var result = orders
    .Where(x => x.Status == OrderStatus.Pending && x.Items.Any())
    .Select(x => new { x.Id, Total = x.Items.Sum(i => i.Quantity * i.UnitPrice) })
    .OrderByDescending(x => x.Total)
    .Take(5)
    .Select(x => x.Id)
    .ToArray();
```

## Correct

```csharp
var pendingOrders = orders.Where(x => x.Status == OrderStatus.Pending && x.Items.Any());
var topOrders = pendingOrders
    .Select(x => new { x.Id, Total = x.CalculateTotal() })
    .OrderByDescending(x => x.Total)
    .Take(5)
    .Select(x => x.Id)
    .ToArray();
```

Or use a loop if it reads better in context.

## Benefits

- Intent is visible sooner.
- Debugging becomes easier.
- Refactoring has lower risk.
- Teams spend less time decoding dense expressions.

## When to Apply

- Complex LINQ chains
- Nested conditional expressions
- Methods where readers must mentally simulate several transformations
