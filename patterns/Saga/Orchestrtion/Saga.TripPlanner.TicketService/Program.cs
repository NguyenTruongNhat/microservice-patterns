using MicroservicePatterns.DatabaseMigrationHelpers;
using Saga.TripPlanner.TicketService.Apis;
using Saga.TripPlanner.TicketService.Bootstraping;
using Saga.TripPlanner.TicketService.Infrastructure.Data;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapTicketApi();

await app.MigrateDbContextAsync<TicketDbContext>();

app.Run();
