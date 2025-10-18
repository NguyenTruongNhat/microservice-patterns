using MicroservicePatterns.DatabaseMigrationHelpers;
using Saga.OnlineStore.InventoryService.APIs;
using Saga.OnlineStore.InventoryService.Bootstraping;
using Saga.OnlineStore.InventoryService.Infrastructure.Data;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapInventoryApi();

await app.MigrateDbContextAsync<InventoryDbContext>();

app.Run();
