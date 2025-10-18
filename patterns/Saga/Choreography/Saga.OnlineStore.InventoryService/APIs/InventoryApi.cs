using Microsoft.AspNetCore.Http.HttpResults;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.InventoryService.Infrastructure.Entity;

namespace Saga.OnlineStore.InventoryService.APIs
{
    public class InventoryApi
    {

        // update the quantity of an inventory item by adding the specified amount
        public static async Task<Results<BadRequest, NotFound, Ok>> Restock([AsParameters] ApiServices services, Guid id, RestockItem item)
        {
            if (item.Quantity <= 0)
            {
                return TypedResults.BadRequest();
            }

            var existingItem = await services.DbContext.Items.FindAsync(id);

            if (existingItem == null)
            {
                return TypedResults.NotFound();
            }
            else
            {
                services.Logger.LogInformation("Restock item: {id}, quantity: {quantity}, existing: {existing}", id, item.Quantity, existingItem.AvailableQuantity);
                var quantityBefore = existingItem.AvailableQuantity;
                existingItem.AvailableQuantity += item.Quantity;

                var quantityAfter = existingItem.AvailableQuantity;
                await services.DbContext.SaveChangesAsync();

                // Optionally, publish an integration event here to notify other services of the quantity change
                await services.EventPublisher.PublishAsync(new ItemQuantityChangedIntegrationEvent()  // not used
                {
                    ItemId = id,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = quantityAfter,
                });
                return TypedResults.Ok();
            }
        }



    }

    public record RestockItem
    {
        public long Quantity { get; set; }
    }
}
