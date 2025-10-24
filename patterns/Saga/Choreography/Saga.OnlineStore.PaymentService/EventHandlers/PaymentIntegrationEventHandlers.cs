using EventBus.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.PaymentService.Infrastructure.Data;

namespace Saga.OnlineStore.PaymentService.EventHandlers
{
    public class PaymentIntegrationEventHandlers(PaymentDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<PaymentIntegrationEventHandlers> logger) :
    INotificationHandler<OrderItemsReservedIntegrationEvent>
    {
        public async Task Handle(OrderItemsReservedIntegrationEvent request, CancellationToken cancellationToken)
        {
            // this event is sent by Payment service when it approves payment for an order
            logger.LogInformation("Handling order items reserved event: {id}", request.OrderId);

            if (request.PaymentCardNumber.Length != 16)
            {
                logger.LogInformation("Payment rejected for OrderId: {OrderId} due to invalid card number", request.OrderId);

                await eventPublisher.PublishAsync(new OrderPaymentRejectedIntegrationEvent
                {
                    OrderId = request.OrderId,
                    Reason = "Invalid payment card number"
                });
                return;
            }

            var card = await dbContext.Cards.Where(c => c.CardNumber == request.PaymentCardNumber).SingleOrDefaultAsync(cancellationToken: cancellationToken);
            if (card == null)
            {
                logger.LogInformation("Payment rejected for OrderId: {OrderId} due to card not found", request.OrderId);

                await eventPublisher.PublishAsync(new OrderPaymentRejectedIntegrationEvent
                {
                    OrderId = request.OrderId,
                    Reason = "Payment card not found"
                });
                return;
            }

            var newBalance = card.Balance - request.Items.Sum(i => i.Price * i.Quantity);
            if (newBalance < 0)
            {
                logger.LogInformation("Payment rejected for OrderId: {OrderId} due to insufficient funds", request.OrderId);
                await eventPublisher.PublishAsync(new OrderPaymentRejectedIntegrationEvent
                {
                    OrderId = request.OrderId,
                    Reason = "Insufficient funds"
                });
                return;
            }
            else
            {
                card.Balance = newBalance;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Payment approved for OrderId: {OrderId}", request.OrderId);
                await eventPublisher.PublishAsync(new OrderPaymentApprovedIntegrationEvent
                {
                    OrderId = request.OrderId
                });
            }
        }
    }
}
