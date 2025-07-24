using CQRS.Library.BorrowingService.Apis;
using CQRS.Library.BorrowingService.Bootstraping;
using CQRS.Library.BorrowingService.Infrastructure.Data;
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

app.MapBorrowingApi();

await app.MigrateDbContextAsync<BorrowingDbContext>();

app.Run();

