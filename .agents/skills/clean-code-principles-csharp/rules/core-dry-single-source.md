---
id: core-dry-single-source
title: DRY - Single Source of Truth
category: core-principles
priority: critical
tags: [DRY, single-source-of-truth, constants, configuration]
related: [core-dry, core-dry-extraction, core-fail-fast]
---

# DRY - Single Source of Truth

Constants, limits, and configuration should have one obvious home. In C#, duplicated values often drift between API layers, hosted services, and builder defaults.

## Incorrect

```csharp
public sealed class CheckoutService
{
    private const decimal TaxRate = 0.08m;
}

public sealed class ReceiptFormatter
{
    private const decimal TaxRate = 0.08m;
}
```

## Correct

```csharp
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public decimal TaxRate { get; init; }
    public decimal FreeShippingThreshold { get; init; }
}

public sealed class CheckoutService
{
    private readonly PricingOptions _pricing;

    public CheckoutService(IOptions<PricingOptions> pricing)
    {
        _pricing = pricing.Value;
    }
}
```

## Benefits

- Values change in one place.
- Configuration becomes easier to validate.
- Testing different scenarios gets simpler.
- Hidden drift between modules disappears.

## When to Apply

- Feature flags, limits, tax rules, retry policies, and builder defaults
