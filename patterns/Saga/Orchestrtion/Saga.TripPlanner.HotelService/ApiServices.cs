using Saga.TripPlanner.HotelService.Apis;
using Saga.TripPlanner.HotelService.Infrastructure.Data;

namespace Saga.TripPlanner.HotelService
{
    public class ApiServices(
    HotelDbContext dbContext,
    ILogger<HotelApi> logger)
    {
        public HotelDbContext DbContext => dbContext;
        public ILogger<HotelApi> Logger { get; } = logger;
    }

}
