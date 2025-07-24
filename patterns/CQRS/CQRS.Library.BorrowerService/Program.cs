using CQRS.Library.BorrowerService.Apis;
using CQRS.Library.BorrowerService.Bootstraping;
using CQRS.Library.BorrowerService.Infrastructure.Data;
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

app.MapBorrowerApi();

await app.MigrateDbContextAsync<BorrowerDbContext>();

app.Run();


