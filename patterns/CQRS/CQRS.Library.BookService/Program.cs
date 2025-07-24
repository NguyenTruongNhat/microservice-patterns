using CQRS.Library.BookService.Apis;
using CQRS.Library.BookService.Bootstraping;
using CQRS.Library.BookService.Infrastructure.Data;
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

app.MapBookApi();

await app.MigrateDbContextAsync<BookDbContext>();

app.Run();
