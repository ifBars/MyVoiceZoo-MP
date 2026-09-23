---
id: solid-dip-abstractions
title: SOLID - Dependency Inversion (Abstractions)
category: solid-principles
priority: critical
tags: [SOLID, DIP, dependency-inversion, abstractions]
related: [solid-dip-injection, solid-ocp-abstraction, pattern-repository]
---

# Dependency Inversion Principle - Abstractions

High-level policy should not depend directly on low-level infrastructure. In C#, this usually means application or domain code should speak in terms of behaviors it needs, not framework types that happen to provide them.

## Incorrect

```csharp
public sealed class InvoiceService
{
    private readonly SqlConnection _connection;

    public InvoiceService(SqlConnection connection)
    {
        _connection = connection;
    }

    public Task MarkPaidAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        // Raw SQL and business policy mixed together
        return Task.CompletedTask;
    }
}
```

## Correct

```csharp
public interface IInvoiceRepository
{
    Task<Invoice?> GetAsync(Guid invoiceId, CancellationToken cancellationToken);
    Task SaveAsync(Invoice invoice, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class InvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public InvoiceService(IInvoiceRepository invoices, IClock clock)
    {
        _invoices = invoices;
        _clock = clock;
    }

    public async Task MarkPaidAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetAsync(invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice not found.");

        invoice.MarkPaid(_clock.UtcNow);
        await _invoices.SaveAsync(invoice, cancellationToken);
    }
}
```

## Benefits

- High-level code expresses business intent.
- Infrastructure can vary without rewriting policies.
- Tests avoid framework-heavy setup.
- Time, IO, and persistence become replaceable seams.

## When to Apply

- Domain or application services
- Code currently coupled to `DbContext`, `SqlConnection`, file IO, or HTTP clients
