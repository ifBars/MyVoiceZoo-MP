---
id: pattern-builder
title: Design Pattern - Fluent Builder
category: design-patterns
priority: high
tags: [design-patterns, builder, fluent-api, api-design, csharp]
related: [core-fail-fast, core-encapsulation, solid-srp-class]
---

# Fluent Builder Pattern

Use a fluent builder when an object has many optional settings, grouped sub-configuration, validation/defaulting behavior, or framework-specific setup details that should not leak into calling code.

This rule takes direct inspiration from the builder style used in `S1API`: small `WithXxx(...)` methods, safe defaults, nested configuration callbacks, and a `Build()` step that validates required state.

A builder should protect a caller from complexity. It should not be a ceremonial wrapper around a simple constructor.

## Incorrect

```csharp
public sealed class NpcDefinition
{
    public NpcDefinition(
        string id,
        string firstName,
        string lastName,
        string iconPath,
        float minWeeklySpend,
        float maxWeeklySpend,
        int minOrdersPerWeek,
        int maxOrdersPerWeek,
        string preferredOrderDay,
        int orderTime,
        string hairPath,
        string faceLayerPath,
        string bodyLayerPath)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        IconPath = iconPath;
        MinWeeklySpend = minWeeklySpend;
        MaxWeeklySpend = maxWeeklySpend;
        MinOrdersPerWeek = minOrdersPerWeek;
        MaxOrdersPerWeek = maxOrdersPerWeek;
        PreferredOrderDay = preferredOrderDay;
        OrderTime = orderTime;
        HairPath = hairPath;
        FaceLayerPath = faceLayerPath;
        BodyLayerPath = bodyLayerPath;
    }

    public string Id { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string IconPath { get; }
    public float MinWeeklySpend { get; }
    public float MaxWeeklySpend { get; }
    public int MinOrdersPerWeek { get; }
    public int MaxOrdersPerWeek { get; }
    public string PreferredOrderDay { get; }
    public int OrderTime { get; }
    public string HairPath { get; }
    public string FaceLayerPath { get; }
    public string BodyLayerPath { get; }
}
```

Problems:

- The call site is hard to read and easy to misuse.
- Related settings are not grouped.
- Validation is unclear.
- Framework-specific details leak into every caller.

## Correct

```csharp
public sealed class NpcDefinitionBuilder
{
    private string? _id;
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string? _iconPath;
    private readonly CustomerDefaultsBuilder _customer = new();
    private readonly AppearanceDefaultsBuilder _appearance = new();

    public NpcDefinitionBuilder WithIdentity(string id, string firstName, string lastName)
    {
        _id = id;
        _firstName = firstName;
        _lastName = lastName;
        return this;
    }

    public NpcDefinitionBuilder WithIcon(string? iconPath)
    {
        _iconPath = iconPath;
        return this;
    }

    public NpcDefinitionBuilder WithCustomerDefaults(Action<CustomerDefaultsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_customer);
        return this;
    }

    public NpcDefinitionBuilder WithAppearanceDefaults(Action<AppearanceDefaultsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_appearance);
        return this;
    }

    public NpcDefinition Build()
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            throw new InvalidOperationException(
                "NpcDefinitionBuilder requires WithIdentity(...) before Build().");
        }

        return new NpcDefinition(
            _id,
            _firstName,
            _lastName,
            _iconPath,
            _customer.Build(),
            _appearance.Build());
    }
}

public sealed class CustomerDefaultsBuilder
{
    private decimal _minWeeklySpend;
    private decimal _maxWeeklySpend = 100m;
    private int _minOrdersPerWeek;
    private int _maxOrdersPerWeek = 2;

    public CustomerDefaultsBuilder WithSpending(decimal minWeekly, decimal maxWeekly)
    {
        _minWeeklySpend = Math.Max(0m, minWeekly);
        _maxWeeklySpend = Math.Max(_minWeeklySpend, maxWeekly);
        return this;
    }

    public CustomerDefaultsBuilder WithOrdersPerWeek(int min, int max)
    {
        _minOrdersPerWeek = Math.Clamp(min, 0, 7);
        _maxOrdersPerWeek = Math.Clamp(Math.Max(min, max), 0, 7);
        return this;
    }

    internal CustomerDefaults Build() => new(_minWeeklySpend, _maxWeeklySpend, _minOrdersPerWeek, _maxOrdersPerWeek);
}
```

## Benefits

- Call sites read like intent, not parameter juggling.
- Defaults stay centralized.
- Validation happens once at the correct boundary.
- Nested builders keep related settings together.
- Engine or framework quirks stay hidden behind the builder.

## When to Apply

- Runtime configuration objects with many optional properties
- Unity or engine-backed objects with awkward setup rules
- APIs that would otherwise need telescoping constructors
- Cases where grouped nested configuration improves readability
- Public mod-authoring APIs where caller intent should stay separate from registry, resolver, prefab, or asset lookup details

## When Not to Apply

- Small immutable records with only a few fields
- Value objects that already fit a single constructor cleanly
- Cases where an object initializer is already obvious and safe
- Pure DTOs where validation happens elsewhere and a builder would only duplicate property assignment

## Boundary Guidance

For S1API-style public APIs, pair builders with focused support types:

- Use `Definition` for stable public descriptions.
- Use `Catalog` for curated built-in entries.
- Use `Registry` for mutable runtime registration.
- Use internal `Resolver` or adapter types for engine object lookup.

Read `../references/s1api-api-design.md` when a builder sits on top of Unity, Schedule One, IL2CPP/Mono, asset loading, network spawn, or save/restore behavior.
