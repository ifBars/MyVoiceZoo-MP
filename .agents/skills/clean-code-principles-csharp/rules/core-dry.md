---
id: core-dry
title: Don't Repeat Yourself (DRY)
category: core-principles
priority: critical
tags: [DRY, duplication, single-source-of-truth, maintainability]
related: [core-dry-extraction, core-dry-single-source, solid-srp-class]
---

# Don't Repeat Yourself (DRY)

Each piece of knowledge should have one authoritative representation. In C#, duplication often appears as repeated validation, repeated mapping logic, or copied business rules spread across handlers and services.

## Incorrect

```csharp
public sealed class UserController
{
    public void Create(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
        {
            throw new ArgumentException("Invalid email.");
        }

        if (request.Password.Length < 8)
        {
            throw new ArgumentException("Password too short.");
        }
    }

    public void Update(UpdateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
        {
            throw new ArgumentException("Invalid email.");
        }

        if (request.Password.Length < 8)
        {
            throw new ArgumentException("Password too short.");
        }
    }
}
```

## Correct

```csharp
public static class UserValidation
{
    public static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            throw new ArgumentException("Invalid email.", nameof(email));
        }
    }

    public static void ValidatePassword(string password)
    {
        if (password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
        }
    }
}
```

## Benefits

- Rules stay consistent.
- Bug fixes happen once.
- Refactoring is safer.
- The codebase has fewer hidden forks of business logic.

## When to Apply

- Repeated validation and mapping logic
- Shared pricing, scheduling, or configuration rules
- Reused builder defaults across similar types
