using Saga.OnlineStore.CatalogService.Bootstraping;
using Saga.OnlineStore.CatalogService.Infrastructure.Data;
using MicroservicePatterns.DatabaseMigrationHelpers;
using Saga.OnlineStore.CatalogService.Apis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapCatalogApi();

await app.MigrateDbContextAsync<CatalogDbContext>();

app.Run();
