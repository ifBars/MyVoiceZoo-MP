---
id: core-dry-extraction
title: DRY - Code Extraction
category: core-principles
priority: critical
tags: [DRY, code-extraction, reuse, refactoring]
related: [core-dry, solid-srp-function, core-kiss-readability]
---

# DRY - Code Extraction

Extract duplicated behavior into a named unit when the duplication represents the same idea. The goal is shared knowledge, not extracting every similar-looking line into a generic helper.

## Incorrect

```csharp
public sealed class OrderMapper
{
    public OrderDto Map(Order order)
    {
        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Items.Sum(x => x.Quantity * x.UnitPrice),
            order.Status.ToString());
    }
}

public sealed class InvoiceMapper
{
    public InvoiceDto Map(Invoice invoice)
    {
        return new InvoiceDto(
            invoice.Id,
            invoice.CustomerId,
            invoice.Items.Sum(x => x.Quantity * x.UnitPrice),
            invoice.Status.ToString());
    }
}
```

## Correct

```csharp
public static class MoneyCalculation
{
    public static decimal CalculateLineItemTotal(IEnumerable<LineItem> items)
    {
        return items.Sum(x => x.Quantity * x.UnitPrice);
    }
}
```

Extract the shared concept, not the entire mapper.

## Benefits

- Shared behavior gets one name.
- Call sites become clearer.
- You avoid accidental divergence.
- Extraction stays aligned to business meaning.

## When to Apply

- The same calculation or rule appears in several places
- You can name the shared concept cleanly

## When Not to Apply

- Similar-looking code changes for different reasons
- The extraction would create an unhelpful generic helper
