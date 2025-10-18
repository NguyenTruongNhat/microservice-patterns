using EventBus.Events;

namespace Saga.OnlineStore.IntegrationEvents;

/// <summary>
/// Not used currently - an example of an integration event that could be published when the quantity of an inventory item changes.
/// </summary>
public class ItemQuantityChangedIntegrationEvent : IntegrationEvent
{
    public Guid ItemId { get; set; }
    public long QuantityBefore { get; set; }
    public long QuantityAfter { get; set; }
}



