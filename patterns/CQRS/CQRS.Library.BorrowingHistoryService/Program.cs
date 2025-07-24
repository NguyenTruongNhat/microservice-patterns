using CQRS.Library.BorrowingHistoryService.Apis;
using CQRS.Library.BorrowingHistoryService.Bootstraping;
using CQRS.Library.BorrowingHistoryService.Infrastructure.Data;
using MicroservicePatterns.DatabaseMigrationHelpers;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapBorrowingHistoryApi();

await app.MigrateDbContextAsync<BorrowingHistoryDbContext>();

app.Run();


