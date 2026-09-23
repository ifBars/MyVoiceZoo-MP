---
id: pattern-repository
title: Design Pattern - Repository
category: design-patterns
priority: high
tags: [design-patterns, repository, data-access, separation-of-concerns]
related: [solid-dip-abstractions, solid-srp-class, core-separation-concerns]
---

# Repository Pattern

Use a repository when persistence is a meaningful architectural boundary. The goal is to keep domain and application logic independent from storage details, not to wrap every ORM call by default.

In C#, this usually matters when domain logic should be testable without a database, when persistence rules are non-trivial, or when a stable contract is useful across application layers.

## Incorrect

```csharp
public sealed class OrderService
{
    private readonly AppDbContext _dbContext;

    public OrderService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = new Order(request.CustomerId);

        foreach (var item in request.Items)
        {
            order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
        }

        _dbContext.Orders.Add(order);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var pendingOrders = await _dbContext.Orders
            .Where(x => x.CustomerId == request.CustomerId && x.Status == OrderStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingOrders.Count > 5)
        {
            throw new InvalidOperationException("Customer has too many pending orders.");
        }

        return order;
    }
}
```

Problems:

- Business logic and persistence details are mixed.
- The service must understand query shape and transaction timing.
- Tests must mimic EF Core behavior or hit a real database.

## Correct

```csharp
public interface IOrderRepository
{
    Task<int> CountPendingForCustomerAsync(Guid customerId, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class OrderService
{
    private readonly IOrderRepository _orders;

    public OrderService(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<Order> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var pendingCount = await _orders.CountPendingForCustomerAsync(request.CustomerId, cancellationToken);
        if (pendingCount > 5)
        {
            throw new InvalidOperationException("Customer has too many pending orders.");
        }

        var order = new Order(request.CustomerId);
        foreach (var item in request.Items)
        {
            order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
        }

        await _orders.AddAsync(order, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
        return order;
    }
}

public sealed class EfCoreOrderRepository : IOrderRepository
{
    private readonly AppDbContext _dbContext;

    public EfCoreOrderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> CountPendingForCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return _dbContext.Orders
            .CountAsync(x => x.CustomerId == customerId && x.Status == OrderStatus.Pending, cancellationToken);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        _dbContext.Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

## Benefits

- Application services stay focused on business rules.
- Query and storage behavior live in one place.
- Tests can swap in an in-memory repository.
- Persistence changes do not ripple through every service.

## When to Apply

- Rich domain behavior with non-trivial queries
- Multiple storage implementations or testing needs
- Clear application/domain boundary requirements

## When Not to Apply

- Thin CRUD endpoints where `DbContext` already is the boundary
- Cases where the repository adds only pass-through methods with no value
