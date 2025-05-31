using CQRS.Library.BorrowingHistoryService.Apis;
using CQRS.Library.BorrowingHistoryService.Bootstraping;

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

app.Run();


