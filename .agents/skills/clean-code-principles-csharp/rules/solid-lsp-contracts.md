---
id: solid-lsp-contracts
title: SOLID - Liskov Substitution (Contracts)
category: solid-principles
priority: critical
tags: [SOLID, LSP, liskov-substitution, contracts]
related: [solid-lsp-preconditions, solid-ocp-abstraction, core-composition]
---

# Liskov Substitution Principle - Contracts

Subtypes must honor the expectations of the base contract. If a subclass throws for normal operations or changes core semantics, the abstraction is lying.

## Incorrect

```csharp
public class DocumentStore
{
    public virtual Task SaveAsync(Document document, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ReadOnlyDocumentStore : DocumentStore
{
    public override Task SaveAsync(Document document, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Read-only store cannot save documents.");
    }
}
```

Any caller expecting a `DocumentStore` that can save now has a broken contract.

## Correct

```csharp
public interface IDocumentReader
{
    Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface IDocumentWriter
{
    Task SaveAsync(Document document, CancellationToken cancellationToken);
}

public sealed class ReadOnlyDocumentStore : IDocumentReader
{
    public Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Document?>(null);
}

public sealed class WritableDocumentStore : IDocumentReader, IDocumentWriter
{
    public Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Document?>(null);
    public Task SaveAsync(Document document, CancellationToken cancellationToken) => Task.CompletedTask;
}
```

## Benefits

- Interfaces tell the truth about capabilities.
- Callers do not need defensive subtype knowledge.
- Read-only and writable behaviors stay explicit.

## When to Apply

- When subclasses throw `NotSupportedException` for ordinary base operations
- When inheritance is hiding incompatible behavior
