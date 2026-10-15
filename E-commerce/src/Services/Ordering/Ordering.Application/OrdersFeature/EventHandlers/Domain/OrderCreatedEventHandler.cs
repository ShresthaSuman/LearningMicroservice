using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Ordering.Application.Extension;
using Ordering.Domain.Events;

namespace Ordering.Application.OrdersFeature.EventHandlers;

public class OrderCreatedEventHandler(IPublishEndpoint publishEndpoint,ILogger<OrderCreatedEventHandler> logger,IFeatureManager featureManager) : INotificationHandler<OrderCreatedEvent>
{
    public  async Task Handle(OrderCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: {DomainEvent}", domainEvent.GetType().Name);

        if (await featureManager.IsEnabledAsync("OrderFulfillment"))
        {
            var oderCreatedIntegrationEvent = domainEvent.order.ToOrderDto();
            await publishEndpoint.Publish(oderCreatedIntegrationEvent, cancellationToken);
        }
     
        
    }
    
}