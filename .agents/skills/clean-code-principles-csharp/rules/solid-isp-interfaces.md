---
id: solid-isp-interfaces
title: SOLID - Interface Segregation (Small Interfaces)
category: solid-principles
priority: critical
tags: [SOLID, ISP, interface-segregation, cohesion]
related: [solid-isp-clients, solid-srp-class, core-separation-concerns]
---

# Interface Segregation Principle - Small Interfaces

Prefer small, cohesive interfaces over broad "god interfaces." In C#, large interfaces often force implementations to depend on methods they do not meaningfully support.

## Incorrect

```csharp
public interface IOrderGateway
{
    Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(Order order, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> SearchAsync(string term, CancellationToken cancellationToken);
    Task ExportAsync(Stream stream, CancellationToken cancellationToken);
    Task RebuildCacheAsync(CancellationToken cancellationToken);
}
```

## Correct

```csharp
public interface IOrderReader
{
    Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> SearchAsync(string term, CancellationToken cancellationToken);
}

public interface IOrderWriter
{
    Task SaveAsync(Order order, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IOrderExporter
{
    Task ExportAsync(Stream stream, CancellationToken cancellationToken);
}
```

## Benefits

- Implementations stay focused.
- Consumers depend on less.
- Capability boundaries are easier to reason about.
- Mocking and testing become simpler.

## When to Apply

- Large service contracts with unrelated verbs
- Interfaces that drive repeated `NotSupportedException` implementations
