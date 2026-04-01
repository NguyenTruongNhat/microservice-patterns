using MicroservicePatterns.DatabaseMigrationHelpers;
using Saga.TripPlanner.TripPlanningService.Apis;
using Saga.TripPlanner.TripPlanningService.Bootstraping;
using Saga.TripPlanner.TripPlanningService.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapTripPlanningApi();

await app.MigrateDbContextAsync<TripPlanningDbContext>();

app.Run();
