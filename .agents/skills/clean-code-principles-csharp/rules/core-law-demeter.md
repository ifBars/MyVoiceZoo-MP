---
id: core-law-demeter
title: Law of Demeter
category: core-principles
priority: critical
tags: [law-of-demeter, coupling, object-navigation]
related: [core-separation-concerns, solid-srp-class, core-encapsulation]
---

# Law of Demeter

Talk to close collaborators, not the full object graph. Long call chains in C# are often a sign that knowledge is leaking across boundaries.

## Incorrect

```csharp
var total = session.CurrentUser.Cart.Items.Sum(x => x.Quantity * x.UnitPrice);
```

## Correct

```csharp
var total = session.GetCheckoutTotal();
```

Or:

```csharp
var total = cart.CalculateTotal();
```

## Benefits

- Less coupling to internal structure.
- Better encapsulation.
- Refactors to nested types become safer.
- Call sites read in domain language.

## When to Apply

- Train-wreck access chains
- Code reaching through several objects just to get one value
