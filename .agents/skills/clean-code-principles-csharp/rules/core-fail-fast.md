---
id: core-fail-fast
title: Fail Fast Principle
category: core-principles
priority: critical
tags: [fail-fast, error-handling, validation]
related: [solid-lsp-preconditions, core-encapsulation]
---

# Fail Fast Principle

Validate invalid state as early as possible and fail with useful messages. In C#, constructors, factories, and `Build()` methods are common places to enforce invariants.

## Incorrect

```csharp
public sealed class ReportBuilder
{
    public string? Name { get; set; }

    public Report Build()
    {
        return new Report(Name!);
    }
}
```

The null problem appears later and farther from the source.

## Correct

```csharp
public sealed class ReportBuilder
{
    private string? _name;

    public ReportBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public Report Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            throw new InvalidOperationException("ReportBuilder requires WithName(...) before Build().");
        }

        return new Report(_name);
    }
}
```

## Benefits

- Invalid state is rejected at the boundary.
- Error messages stay specific.
- Bugs surface closer to their source.
- Callers learn the correct usage contract immediately.

## When to Apply

- Builders and factories
- Domain constructors and state transitions
- Configuration loading and options binding
