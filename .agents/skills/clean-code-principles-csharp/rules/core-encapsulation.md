---
id: core-encapsulation
title: Encapsulation
category: core-principles
priority: critical
tags: [encapsulation, information-hiding, api-design]
related: [core-law-demeter, solid-srp-class, core-fail-fast]
---

# Encapsulation

Keep a type's invariants inside the type. Public setters and mutable collections often let outside code bypass important rules.

## Incorrect

```csharp
public sealed class Order
{
    public OrderStatus Status { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}
```

Any caller can mutate state into invalid combinations.

## Correct

```csharp
public sealed class Order
{
    private readonly List<OrderItem> _items = new();

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public IReadOnlyList<OrderItem> Items => _items;

    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("Cannot add items after processing starts.");
        }

        _items.Add(new OrderItem(productId, quantity, unitPrice));
    }

    public void MarkPaid()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("Only pending orders can be paid.");
        }

        Status = OrderStatus.Paid;
    }
}
```

## Benefits

- Invalid state becomes harder to create.
- Rules live with the data they govern.
- Refactoring internal representation becomes safer.
- Public APIs express intent instead of raw mutation.

## When to Apply

- Entities and aggregates
- Builders exposing intermediate mutable state
- Types with important state transitions or collection invariants
