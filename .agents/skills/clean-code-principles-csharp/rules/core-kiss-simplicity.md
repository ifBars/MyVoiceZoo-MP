---
id: core-kiss-simplicity
title: KISS - Simplicity
category: core-principles
priority: critical
tags: [KISS, simplicity, overengineering, maintainability]
related: [core-kiss-readability, core-yagni-abstractions, solid-srp-function]
---

# KISS - Simplicity

Prefer the simplest design that solves the current problem well. In C#, overengineering often shows up as generic frameworks, unnecessary inheritance, or abstraction layers created before the second use case exists.

## Incorrect

```csharp
public interface IHandler<TRequest, TResponse>
{
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}

public sealed class AddItemToCartHandler : IHandler<AddItemToCartRequest, AddItemToCartResponse>
{
    public Task<AddItemToCartResponse> ExecuteAsync(AddItemToCartRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AddItemToCartResponse(true));
    }
}
```

For one use case, the abstraction buys nothing.

## Correct

```csharp
public sealed class CartService
{
    public Task AddItemAsync(AddItemToCartRequest request, CancellationToken cancellationToken)
    {
        // Straightforward behavior for the current need.
        return Task.CompletedTask;
    }
}
```

## Benefits

- Less code to maintain.
- Fewer abstraction leaks.
- Lower cognitive load for future readers.
- Easier refactoring when real variation appears.

## When to Apply

- Before introducing reusable frameworks or generic base classes
- When a simple class or method already fits the problem
