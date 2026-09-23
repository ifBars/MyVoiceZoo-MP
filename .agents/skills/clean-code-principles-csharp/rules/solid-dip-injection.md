---
id: solid-dip-injection
title: SOLID - Dependency Inversion (Injection)
category: solid-principles
priority: critical
tags: [SOLID, DIP, dependency-injection, testability]
related: [solid-dip-abstractions, solid-srp-class, core-composition]
---

# Dependency Inversion Principle - Dependency Injection

Inject dependencies from the outside instead of constructing them inside the class. In C#, constructor injection is usually the clearest default because it makes required collaborators explicit and keeps composition at the edge.

## Incorrect

```csharp
public sealed class UserRegistrationService
{
    private readonly SqlConnection _connection;
    private readonly PasswordHasher _hasher;
    private readonly SmtpClient _smtpClient;

    public UserRegistrationService()
    {
        _connection = new SqlConnection(Environment.GetEnvironmentVariable("DefaultConnection"));
        _hasher = new PasswordHasher();
        _smtpClient = new SmtpClient("smtp.example.com");
    }

    public async Task RegisterAsync(string email, string password)
    {
        var hashedPassword = _hasher.Hash(password);
        await _connection.OpenAsync();
        await _smtpClient.SendMailAsync("noreply@example.com", email, "Welcome", "Thanks for registering.");
    }
}
```

Problems:

- The service owns creation, configuration, and business logic.
- Tests require real infrastructure or awkward shims.
- Environment-specific wiring is scattered inside application code.

## Correct

```csharp
public interface IUserRepository
{
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
}

public interface IEmailSender
{
    Task SendWelcomeAsync(string email, CancellationToken cancellationToken);
}

public sealed class UserRegistrationService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UserRegistrationService> _logger;

    public UserRegistrationService(
        IUserRepository users,
        IPasswordHasher hasher,
        IEmailSender emailSender,
        ILogger<UserRegistrationService> logger)
    {
        _users = users;
        _hasher = hasher;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (await _users.ExistsAsync(email, cancellationToken))
        {
            throw new InvalidOperationException("User already exists.");
        }

        var user = new User(email, _hasher.Hash(password));
        await _users.AddAsync(user, cancellationToken);
        await _emailSender.SendWelcomeAsync(email, cancellationToken);

        _logger.LogInformation("Registered user {Email}", email);
    }
}
```

## Benefits

- Required collaborators are explicit.
- Tests can use fakes or mocks.
- Composition moves to startup or the composition root.
- Services stay focused on behavior instead of wiring.

## When to Apply

- Application services
- Hosted services and background workers
- Controllers and endpoints with real infrastructure dependencies
- Unity or modding wrappers that should hide framework setup
