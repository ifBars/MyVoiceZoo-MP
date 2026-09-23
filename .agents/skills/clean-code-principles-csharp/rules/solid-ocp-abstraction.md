---
id: solid-ocp-abstraction
title: SOLID - Open/Closed (Abstraction)
category: solid-principles
priority: critical
tags: [SOLID, OCP, open-closed, abstraction]
related: [solid-ocp-extension, solid-dip-abstractions, pattern-repository]
---

# Open/Closed Principle - Abstraction

Create extension seams around the dimension that actually changes. In C#, a focused abstraction is often better than subclassing a large service just to override one method.

## Incorrect

```csharp
public class NotificationService
{
    public virtual Task SendAsync(Message message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class EmailNotificationService : NotificationService
{
    public override Task SendAsync(Message message, CancellationToken cancellationToken)
    {
        return _emailClient.SendAsync(message.Email, message.Subject, message.Body, cancellationToken);
    }
}

public sealed class SmsNotificationService : NotificationService
{
    public override Task SendAsync(Message message, CancellationToken cancellationToken)
    {
        return _smsClient.SendAsync(message.PhoneNumber, message.Body, cancellationToken);
    }
}
```

The shared base type does not model a coherent contract.

## Correct

```csharp
public interface INotificationChannel
{
    bool CanSend(Message message);
    Task SendAsync(Message message, CancellationToken cancellationToken);
}

public sealed class NotificationDispatcher
{
    private readonly IEnumerable<INotificationChannel> _channels;

    public NotificationDispatcher(IEnumerable<INotificationChannel> channels)
    {
        _channels = channels;
    }

    public Task SendAsync(Message message, CancellationToken cancellationToken)
    {
        var channel = _channels.FirstOrDefault(x => x.CanSend(message))
            ?? throw new InvalidOperationException("No notification channel can handle the message.");

        return channel.SendAsync(message, cancellationToken);
    }
}
```

## Benefits

- The abstraction matches the changing behavior.
- New channels plug in cleanly.
- Avoids fragile inheritance hierarchies.
- Makes capability boundaries explicit.

## When to Apply

- When adding a new variant should not require touching core orchestration
- When subclassing exists only to override one small area of behavior
