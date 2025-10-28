using Saga.TripPlanner.PaymentService.Infrastructure.Data;
using MicroservicePatterns.DatabaseMigrationHelpers;
using Saga.TripPlanner.PaymentService.Bootstraping;
using Saga.TripPlanner.PaymentService.Apis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapPaymentApi();

await app.MigrateDbContextAsync<PaymentDbContext>();

app.Run();
