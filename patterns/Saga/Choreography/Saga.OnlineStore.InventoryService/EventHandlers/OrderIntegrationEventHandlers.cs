using EventBus.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.InventoryService.Infrastructure.Data;
using Saga.OnlineStore.InventoryService.Infrastructure.Entity;

namespace Saga.OnlineStore.InventoryService.EventHandlers
{
    public class OrderIntegrationEventHandlers(InventoryDbContext dbContext,
        IEventPublisher eventPublisher,
        ILogger<ProductIntegrationEventHandlers> logger) :
        IRequestHandler<OrderPlacedIntegrationEvent>,
        IRequestHandler<OrderPaymentRejectedIntegrationEvent>
    {
        public async Task Handle(OrderPlacedIntegrationEvent request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Received OrderPlacedIntegrationEvent for OrderId: {OrderId}", request.OrderId);
            try
            {
                foreach (var requestItem in request.Items)
                {
                    var itemInInventory = await dbContext.Items.Where(item => item.Id == requestItem.ProductId).SingleOrDefaultAsync(cancellationToken: cancellationToken);
                    if (itemInInventory != null)
                    {
                        logger.LogInformation("Processing item {ProductId} with quantity {Quantity}", requestItem.ProductId, requestItem.Quantity);
                        if (itemInInventory.AvailableQuantity >= requestItem.Quantity)
                        {
                            itemInInventory.AvailableQuantity -= requestItem.Quantity;
                            logger.LogInformation("Reserved {Quantity} of ProductId {ProductId}. New available quantity: {AvailableQuantity}", requestItem.Quantity, requestItem.ProductId, itemInInventory.AvailableQuantity);

                            dbContext.ReservedItems.Add(new ReservedItem()
                            {
                                Id = Guid.CreateVersion7(),
                                ItemId = requestItem.ProductId,
                                OrderId = request.OrderId,
                                Quantity = requestItem.Quantity,
                            });
                        }
                        else
                        {
                            await eventPublisher.PublishAsync(new OrderItemsReservationFailedIntegrationEvent
                            {
                                OrderId = request.OrderId,
                                Reason = $"Item stock too low: {requestItem.ProductId}"
                            });
                            return;
                        }
                    }
                    else
                    {
                        await eventPublisher.PublishAsync(new OrderItemsReservationFailedIntegrationEvent
                        {
                            OrderId = request.OrderId,
                            Reason = $"Item not found in inventory: {requestItem.ProductId}"
                        });
                        return;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await eventPublisher.PublishAsync(new OrderItemsReservedIntegrationEvent(request));
                logger.LogInformation("All items reserved successfully for OrderId: {OrderId}", request.OrderId);
            }
            catch (Exception)
            {
                logger.LogError("Error occurred while reserving items for OrderId: {OrderId}", request.OrderId);
                await eventPublisher.PublishAsync(new OrderItemsReservationFailedIntegrationEvent
                {
                    OrderId = request.OrderId,
                    Reason = "Internal error during reservation"
                });
            }
        }

        public async Task Handle(OrderPaymentRejectedIntegrationEvent request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Received OrderPaymentRejectedIntegrationEvent for OrderId: {OrderId}", request.OrderId);
            var reservedItems = await dbContext.ReservedItems.Where(ri => ri.OrderId == request.OrderId)
                                                             .ToListAsync(cancellationToken: cancellationToken);
           foreach (var reservedItem in reservedItems)
            {
                var itemInInventory = await dbContext.Items.Where(item => item.Id == reservedItem.ItemId)
                                                          .SingleOrDefaultAsync(cancellationToken: cancellationToken);
                if (itemInInventory != null)
                {
                    itemInInventory.AvailableQuantity += reservedItem.Quantity;
                    dbContext.ReservedItems.Remove(reservedItem);
                    logger.LogInformation("Released {Quantity} of ProductId {ProductId}. New available quantity: {AvailableQuantity}", reservedItem.Quantity, reservedItem.ItemId, itemInInventory.AvailableQuantity);
                }
            }
            dbContext.ReservedItems.RemoveRange(reservedItems);

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("All reserved items released for OrderId: {OrderId}", request.OrderId);
        }
    }
}
