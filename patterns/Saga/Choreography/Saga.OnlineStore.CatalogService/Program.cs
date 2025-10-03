using Saga.OnlineStore.CatalogService.Bootstraping;
using Saga.OnlineStore.CatalogService.Infrastructure.Data;
using MicroservicePatterns.DatabaseMigrationHelpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

await app.MigrateDbContextAsync<CatalogDbContext>();

app.Run();
