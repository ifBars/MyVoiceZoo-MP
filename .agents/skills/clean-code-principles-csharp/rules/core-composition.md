---
id: core-composition
title: Composition Over Inheritance
category: core-principles
priority: critical
tags: [composition, inheritance, flexibility, design]
related: [solid-lsp-contracts, solid-dip-abstractions, core-separation-concerns]
---

# Composition Over Inheritance

Prefer assembling behavior from smaller collaborators over building deep inheritance trees. In C#, composition usually gives better testability, fewer fragile overrides, and clearer extension seams.

## Incorrect

```csharp
public class Enemy
{
    public virtual void Attack() { }
}

public class FlyingEnemy : Enemy
{
    public override void Attack() { }
}

public class PoisonFlyingEnemy : FlyingEnemy
{
    public override void Attack() { }
}
```

## Correct

```csharp
public interface IAttackBehavior
{
    void Attack();
}

public interface IMovementBehavior
{
    void Move();
}

public sealed class Enemy
{
    private readonly IAttackBehavior _attack;
    private readonly IMovementBehavior _movement;

    public Enemy(IAttackBehavior attack, IMovementBehavior movement)
    {
        _attack = attack;
        _movement = movement;
    }
}
```

## Benefits

- Behavior combinations stay flexible.
- Smaller parts are easier to test.
- You avoid override chains and LSP problems.
- New combinations become additive.

## When to Apply

- Feature toggles and policy selection
- Gameplay behaviors, validators, formatters, and strategies
