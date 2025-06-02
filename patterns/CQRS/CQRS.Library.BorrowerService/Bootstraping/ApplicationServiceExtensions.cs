using CQRS.Library.BorrowerService.Infrastructure.Data;
using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;


namespace CQRS.Library.BorrowerService.Bootstraping;
public static class ApplicationServiceExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();

        // Add EF Core
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<BorrowerDbContext>(options => options.UseNpgsql(connectionString));

        builder.Services.ConfigureKafkaProducer(builder.Configuration);

        builder.AddKafkaEventPublisher("BorrowerServiceTP");

        return builder;
    }
}
