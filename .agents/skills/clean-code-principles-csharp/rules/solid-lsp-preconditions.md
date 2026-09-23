---
id: solid-lsp-preconditions
title: SOLID - Liskov Substitution (Preconditions)
category: solid-principles
priority: critical
tags: [SOLID, LSP, liskov-substitution, preconditions, postconditions]
related: [solid-lsp-contracts, core-fail-fast, solid-ocp-abstraction]
---

# Liskov Substitution Principle - Preconditions

A subtype must not require stricter inputs than the base contract. If callers must know the exact subtype to call it safely, substitution is broken.

## Incorrect

```csharp
public class DiscountPolicy
{
    public virtual decimal Apply(Customer customer, decimal total)
    {
        return total * 0.95m;
    }
}

public sealed class VipDiscountPolicy : DiscountPolicy
{
    public override decimal Apply(Customer customer, decimal total)
    {
        if (!customer.IsVip)
        {
            throw new InvalidOperationException("VIP discount requires a VIP customer.");
        }

        return total * 0.85m;
    }
}
```

## Correct

```csharp
public interface IDiscountPolicy
{
    bool CanApply(Customer customer);
    decimal Apply(Customer customer, decimal total);
}

public sealed class VipDiscountPolicy : IDiscountPolicy
{
    public bool CanApply(Customer customer) => customer.IsVip;

    public decimal Apply(Customer customer, decimal total)
    {
        return total * 0.85m;
    }
}
```

## Benefits

- Capability checks move into the contract.
- Callers can select policies safely.
- Subtypes stay honest about input requirements.

## When to Apply

- Policy selection
- Validation-heavy strategy implementations
- Inheritance trees where child types reject valid base inputs
