---
id: core-yagni-features
title: YAGNI - Features
category: core-principles
priority: critical
tags: [YAGNI, speculative-features, lean-development]
related: [core-yagni-abstractions, core-kiss-simplicity]
---

# YAGNI - Features

Do not build features because they might be useful later. In C#, speculative work often becomes extra flags, unused states, and partially implemented extension points that never pay for their maintenance cost.

## Incorrect

```csharp
public sealed class DiscountRule
{
    public bool SupportsCoupons { get; init; }
    public bool SupportsFlashSales { get; init; }
    public bool SupportsRegionalOverrides { get; init; }
    public bool SupportsPartnerPricing { get; init; }
}
```

None of these features are currently used.

## Correct

```csharp
public sealed class DiscountRule
{
    public decimal Percentage { get; init; }
}
```

Add new behavior when the use case becomes real.

## Benefits

- Smaller change surface.
- Less dead code and fewer dormant bugs.
- Easier reasoning about the current system.
- Faster delivery of the feature that actually matters now.

## When to Apply

- Optional flags with no active caller
- Placeholder branches for future requirements
- Extra persistence fields with no current meaning
