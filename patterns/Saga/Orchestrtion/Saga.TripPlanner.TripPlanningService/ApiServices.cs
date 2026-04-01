using EventBus.Abstractions;
using Saga.TripPlanner.TripPlanningService.Apis;
using Saga.TripPlanner.TripPlanningService.Infrastructure.Data;

namespace Saga.TripPlanner.TripPlanningService
{
    public class ApiServices(
    TripPlanningDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<TripPlanningApi> logger)
    {
        public TripPlanningDbContext DbContext => dbContext;
        public IEventPublisher EventPublisher => eventPublisher;
        public ILogger<TripPlanningApi> Logger { get; } = logger;
    }

}
