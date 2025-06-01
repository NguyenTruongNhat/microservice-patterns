using CQRS.Library.BookService.Infrastructure.Data;
using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;

namespace CQRS.Library.BookService.Bootstraping;

public static class ApplicationServiceExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();

        // Add EF Core
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<BookDbContext>(options => options.UseNpgsql(connectionString));

        builder.Services.ConfigureKafkaProducer(builder.Configuration);

        builder.AddKafkaEventPublisher("BookServiceTP");

        return builder;
    }

}
