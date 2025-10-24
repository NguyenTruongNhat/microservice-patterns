using EventBus.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.OrderService.Infrastructure.Data;

namespace Saga.OnlineStore.OrderService.EventHandlers
{
    public class OrderIntegrationEventHandlers(OrderDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<OrderIntegrationEventHandlers> logger) :
    INotificationHandler<OrderItemsReservationFailedIntegrationEvent>,
    INotificationHandler<OrderPaymentApprovedIntegrationEvent>,
    INotificationHandler<OrderPaymentRejectedIntegrationEvent>
    {
        public async Task Handle(OrderItemsReservationFailedIntegrationEvent request, CancellationToken cancellationToken)
        {
            // this event is sent by Inventory service when it fails to reserve items for an order
            logger.LogInformation("Handling order items reservation failed event: {OrderId}, Reason: {Reason}", request.OrderId, request.Reason);

            await RejectOrder(request.OrderId, request.Reason, cancellationToken);
        }


        public async Task Handle(OrderPaymentRejectedIntegrationEvent request, CancellationToken cancellationToken)
        {
            // this event is sent by Payment service when it rejects payment for an order
            logger.LogInformation("Handling order payment rejected event: {id}", request.OrderId);

            await RejectOrder(request.OrderId, "Payment rejected", cancellationToken);
        }

        private async Task RejectOrder(Guid orderId, string reason, CancellationToken cancellationToken)
        {
            var order = await dbContext.Orders.Where(o => o.Id == orderId).SingleOrDefaultAsync(cancellationToken);
            if (order == null)
            {
                logger.LogWarning("Order with Id {OrderId} not found", orderId);
                return;
            }

            order.Status = Infrastructure.Entity.OrderStatus.Rejected;
            order.StatusMessage = reason;

            logger.LogInformation("Order with Id {OrderId} rejected. Reason: {Reason}", orderId, reason);

            await eventPublisher.PublishAsync(new OrderRejectedIntegrationEvent()
            {
                OrderId = orderId,
                Reason = reason
            });
        }


        public async Task Handle(OrderPaymentApprovedIntegrationEvent notification, CancellationToken cancellationToken)
        {
            // this event is sent by Payment service when it approves payment for an order
            logger.LogInformation("Handling order payment approved event: {id}", notification.OrderId);

            var order = await dbContext.Orders.Where(o => o.Id == notification.OrderId).SingleOrDefaultAsync(cancellationToken);
            if (order == null)
            {
                logger.LogWarning("Order with Id {OrderId} not found", notification.OrderId);
                return;
            }
            order.Status = Infrastructure.Entity.OrderStatus.Created;
            await dbContext.SaveChangesAsync(cancellationToken);

            await eventPublisher.PublishAsync(new OrderApprovedIntegrationEvent()
            {
                OrderId = notification.OrderId
            });
        }
    }
}
