---
id: solid-ocp-extension
title: SOLID - Open/Closed Principle (Extension)
category: solid-principles
priority: critical
tags: [SOLID, OCP, open-closed, extensibility, design-patterns]
related: [solid-ocp-abstraction, pattern-repository, solid-dip-abstractions]
---

# Open/Closed Principle - Extension

Stable code should be open for new behavior without needing edits for every new variant. In C#, repeated `switch` growth is often a signal that an extension seam is missing.

## Incorrect

```csharp
public sealed class ReportExporter
{
    public byte[] Export(Report report, string format)
    {
        return format switch
        {
            "pdf" => ExportPdf(report),
            "csv" => ExportCsv(report),
            "json" => ExportJson(report),
            _ => throw new NotSupportedException($"Unsupported format '{format}'.")
        };
    }
}
```

Every new format requires editing `ReportExporter`.

## Correct

```csharp
public interface IReportFormatter
{
    string Format { get; }
    byte[] Export(Report report);
}

public sealed class ReportExporter
{
    private readonly IReadOnlyDictionary<string, IReportFormatter> _formatters;

    public ReportExporter(IEnumerable<IReportFormatter> formatters)
    {
        _formatters = formatters.ToDictionary(x => x.Format, StringComparer.OrdinalIgnoreCase);
    }

    public byte[] Export(Report report, string format)
    {
        if (!_formatters.TryGetValue(format, out var formatter))
        {
            throw new NotSupportedException($"Unsupported format '{format}'.");
        }

        return formatter.Export(report);
    }
}
```

## Benefits

- New behavior is additive instead of invasive.
- Existing code paths stay stable.
- Variants can be tested independently.
- Registration becomes an application concern instead of core logic.

## When to Apply

- Plugin-style behavior
- Formatters, strategies, validators, and policy families
- Cases where new variants appear regularly
