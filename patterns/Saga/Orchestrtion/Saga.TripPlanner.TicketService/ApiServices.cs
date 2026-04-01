using Saga.TripPlanner.TicketService.Infrastructure.Data;
using static Saga.TripPlanner.TicketService.Apis.TicketApiExtentions;

namespace Saga.TripPlanner.TicketService;
public class ApiServices(
    TicketDbContext dbContext,
    ILogger<TicketApi> logger)
{
    public TicketDbContext DbContext => dbContext;
    public ILogger<TicketApi> Logger { get; } = logger;
}
