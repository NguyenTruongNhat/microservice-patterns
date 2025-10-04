using MicroservicePatterns.DatabaseMigrationHelpers;
using Saga.OnlineStore.OrderService.Apis;
using Saga.OnlineStore.OrderService.Bootstraping;
using Saga.OnlineStore.OrderService.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapOrderApi();

await app.MigrateDbContextAsync<OrderDbContext>();

app.Run();
