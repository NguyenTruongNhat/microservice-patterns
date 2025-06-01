using CQRS.Library.BorrowingHistoryService.Infrastructure.Data;
using CQRS.Library.IntegrationEvents;
using EventBus;
using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;

namespace CQRS.Library.BorrowingHistoryService.Bootstraping;
public static class ApplicationServiceExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();

        // Add EF Core
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<BorrowingHistoryDbContext>(options => options.UseNpgsql(connectionString));

        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        });

        var eventConsumingTopics = new List<string> {
                                        "BookServiceTP",
                                        "BorrowerServiceTP",
                                        "BorrowingServiceTP"};

        if (eventConsumingTopics.Count > 0)
        {
            builder.AddKafkaEventConsumer(options =>
            {
                options.ServiceName = "BorrowingHistoryService";
                options.KafkaGroupId = "cqrs";
                options.Topics.AddRange(eventConsumingTopics);
                options.IntegrationEventFactory = IntegrationEventFactory<BookCreatedIntegrationEvent>.Instance;
            });
        }

        return builder;
    }
}

