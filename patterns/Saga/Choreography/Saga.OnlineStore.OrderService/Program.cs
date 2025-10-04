using Saga.OnlineStore.OrderService.Bootstraping;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOpenApi();

builder.AddApplicationServices();

var app = builder.Build();

app.UseHttpsRedirection();

app.Run();
