---
id: solid-srp-class
title: SOLID - Single Responsibility Principle (Class Level)
category: solid-principles
priority: critical
tags: [SOLID, SRP, single-responsibility, class-design]
related: [solid-srp-function, core-separation-concerns, solid-isp-interfaces]
---

# Single Responsibility Principle - Class Level

A class should have one reason to change. In C#, a class that validates input, talks to the database, formats emails, and logs everything is usually hiding multiple responsibilities behind one type.

## Incorrect

```csharp
public sealed class UserManager
{
    public async Task<User> CreateAsync(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
        {
            throw new ArgumentException("Invalid email.", nameof(request));
        }

        using var connection = new SqlConnection("...");
        await connection.OpenAsync();

        var user = new User(request.Email, request.Password);
        Console.WriteLine($"Creating user {user.Email}");

        using var client = new SmtpClient("smtp.example.com");
        await client.SendMailAsync("noreply@example.com", user.Email, "Welcome", "Hi");

        return user;
    }
}
```

## Correct

```csharp
public sealed class UserValidator
{
    public void Validate(CreateUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
        {
            throw new ArgumentException("Invalid email.", nameof(request));
        }
    }
}

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);
}

public interface IWelcomeEmailSender
{
    Task SendAsync(User user, CancellationToken cancellationToken);
}

public sealed class UserRegistrationService
{
    private readonly UserValidator _validator;
    private readonly IUserRepository _users;
    private readonly IWelcomeEmailSender _welcomeEmail;

    public UserRegistrationService(UserValidator validator, IUserRepository users, IWelcomeEmailSender welcomeEmail)
    {
        _validator = validator;
        _users = users;
        _welcomeEmail = welcomeEmail;
    }

    public async Task<User> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        _validator.Validate(request);
        var user = new User(request.Email, request.Password);
        await _users.AddAsync(user, cancellationToken);
        await _welcomeEmail.SendAsync(user, cancellationToken);
        return user;
    }
}
```

## Benefits

- Smaller classes are easier to test.
- Dependencies become clearer.
- Changes in validation, persistence, or messaging stay isolated.
- Responsibilities can evolve independently.

## When to Apply

- Services with growing constructor dependency lists
- Unity components mixing gameplay, persistence, and presentation
- Builders that start owning unrelated orchestration logic
