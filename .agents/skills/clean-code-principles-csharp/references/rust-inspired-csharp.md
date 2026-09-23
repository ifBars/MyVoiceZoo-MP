# Rust-Inspired C# Boundaries

Use this reference when the user asks for scalable, organized C# code inspired by Rust projects. Apply the architectural habits that transfer well. Do not make C# look like Rust for its own sake.

## Transfer the Discipline, Not the Syntax

Rust habits that translate well to C#:

- Small modules with clear ownership
- Private implementation details and explicit public exports
- Domain types that make invalid states harder to express
- Methods that preserve invariants instead of public mutation
- Boundary adapters for external systems
- Tests for parsing, validation, lifecycle transitions, and invariants

Rust habits that usually do not translate directly:

- Forcing ownership terminology into C# design
- Creating wrapper types without a repeated invariant
- Replacing idiomatic C# exceptions, options, or DI with Rust-shaped abstractions
- Splitting files so aggressively that navigation becomes worse

## Public Surface Discipline

Rust crates often expose a curated facade from `lib.rs`. In C#, use the same idea through namespace and assembly boundaries:

- Keep helper classes `internal` by default.
- Expose public types only when callers need them.
- Prefer one obvious entry point for a feature.
- Re-export or document the stable public surface instead of forcing callers through implementation folders.
- Keep public names domain-focused, not implementation-focused.

Good C# equivalents:

```csharp
public sealed record AssetKey(string Value);

public sealed class AssetCatalog
{
    private readonly Dictionary<AssetKey, AssetDefinition> assets = new();

    public bool TryGet(AssetKey key, out AssetDefinition definition) =>
        assets.TryGetValue(key, out definition);
}
```

Use a wrapper like `AssetKey` when the concept repeats across the API or carries validation. Do not wrap every string by default.

## Module and File Size Pressure

Rust projects often keep pressure on large files with explicit exceptions. In C#, do not enforce line counts blindly, but treat very large files as a review signal:

- Is the file mixing API, runtime glue, validation, caching, and UI?
- Can nested concerns move to internal helpers without fragmenting the public surface?
- Are exceptions documented because the slice is temporarily cohesive, or are they stale?
- Are tests co-located by behavior rather than dumped into one broad fixture?

Use this as a maintainability heuristic, not a hard rule for generated code, Unity partials, or framework-required files.

## Invariant Ownership

Prefer methods that preserve state:

```csharp
public sealed class GameSettings
{
    private int uiScalePercent = 100;

    public int UiScalePercent => uiScalePercent;

    public void SetUiScalePercent(int value)
    {
        uiScalePercent = Math.Clamp(value, 75, 200);
    }
}
```

Avoid public setters when callers can bypass the rule:

```csharp
public int UiScalePercent { get; set; }
```

This is the same design pressure as Rust constructors and methods that maintain valid states.

## Composition and Commands

Rust code often uses traits to describe capability boundaries. In C#, use interfaces or abstract base types only when there is a real polymorphic boundary:

- Command history, undo/redo actions, policy objects, and external adapters are good candidates.
- Data containers and one-off services usually are not.

Prefer capability-sized abstractions:

```csharp
public interface IEditorCommand<TDocument>
{
    string Label { get; }
    bool Changed { get; }
    void Apply(TDocument document);
    void Undo(TDocument document);
}
```

Avoid a broad interface that forces unrelated methods onto implementers.

## Testing Guidance

Add focused tests for:

- Clamping and normalization
- Required field validation
- ID/key parsing and lookup
- Registry duplicate handling
- Builder `Build()` failure paths
- Public facade behavior that should remain stable during internal refactors

Prefer tests that encode the boundary contract. Avoid tests that only mirror implementation steps.

## Review Checklist

- Is the public surface intentionally small?
- Are helper types internal unless callers need them?
- Does each wrapper type remove a real invalid-state or readability problem?
- Is mutation behind methods that preserve invariants?
- Are framework details isolated behind adapters, resolvers, or composition roots?
- Do tests cover behavior and invariants instead of incidental implementation?
- Is the design idiomatic C# even if the boundary discipline came from Rust?
