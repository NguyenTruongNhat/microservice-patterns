using Saga.OnlineStore.PaymentService.APIs;
using Saga.OnlineStore.PaymentService.Bootstraping;
using Saga.OnlineStore.PaymentService.Infrastructure.Data;
using MicroservicePatterns.DatabaseMigrationHelpers;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapPaymentApi();

await app.MigrateDbContextAsync<PaymentDbContext>();

app.Run();
