using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.CatalogService.Infrastructure.Data;
using EventBus.Kafka;


namespace Saga.OnlineStore.CatalogService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();

            // Add EF Core
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));

            builder.Services.ConfigureKafkaProducer(builder.Configuration);
            builder.AddKafkaEventPublisher("Saga-OnlineStore-CatalogService");

            return builder;
        }
    }
}
