using Saga.TripPlanner.HotelService.Infrastructure.Data;
using MicroservicePatterns.DatabaseMigrationHelpers;
using Saga.TripPlanner.HotelService.Bootstraping;
using Saga.TripPlanner.HotelService.Apis;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapHotelApi();

await app.MigrateDbContextAsync<HotelDbContext>();

app.Run();
