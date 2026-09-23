---
id: core-yagni-abstractions
title: YAGNI - Abstractions
category: core-principles
priority: critical
tags: [YAGNI, premature-abstraction, simplicity]
related: [core-yagni-features, core-kiss-simplicity, solid-ocp-abstraction]
---

# YAGNI - Abstractions

Do not introduce abstractions before you know what varies. In C#, generic repositories, command buses, and handler frameworks are common examples of abstractions that appear before the second real use case exists.

## Incorrect

```csharp
public interface IRepository<T>
{
    Task<T?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(T entity, CancellationToken cancellationToken);
}

public sealed class CustomerRepository : IRepository<Customer>
{
    public Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Customer?>(null);
    public Task SaveAsync(Customer entity, CancellationToken cancellationToken) => Task.CompletedTask;
}
```

## Correct

```csharp
public interface ICustomerRepository
{
    Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(Customer customer, CancellationToken cancellationToken);
}
```

Start with the domain-specific abstraction you actually need.

## Benefits

- Abstractions stay meaningful.
- Callers read domain language instead of framework language.
- You avoid broad generic contracts that age poorly.
- Refactoring remains easier when real variation appears.

## When to Apply

- Generic base services with only one implementation
- Reusable frameworks built before repeated use proves the need
