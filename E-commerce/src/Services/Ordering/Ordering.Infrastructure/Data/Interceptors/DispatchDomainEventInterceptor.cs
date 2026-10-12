using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ordering.Domain.Abstraction;

namespace Ordering.Infrastructure.Data.Interceptors;

public class DispatchDomainEventInterceptor(IMediator mediator) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        DispatchDomainEvents(eventData.Context).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public  override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new CancellationToken())
    {
        await DispatchDomainEvents(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public async Task DispatchDomainEvents(DbContext? context)
    {
        if (context is null) return;
        
        //get all the aggregator entity if they have any domain events
        var aggregator = context.ChangeTracker
            .Entries<IAggregate>()
            .Where(i => i.Entity.DomainEvent.Any())
            .Select(x => x.Entity);
        
        //list all the domain events from the aggregator
        var domainEvents = aggregator.SelectMany(a=>a.DomainEvent).ToList();
        
        aggregator.ToList().ForEach(a=>a.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
           await mediator.Publish(domainEvent);
        }
       
    }
}