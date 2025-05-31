using MicroservicePatterns.AppHost.Extentions;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddApplicationServices();

builder.Build().Run();
