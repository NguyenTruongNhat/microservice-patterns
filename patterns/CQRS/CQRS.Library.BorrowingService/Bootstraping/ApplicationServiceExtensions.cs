using CQRS.Library.BorrowingService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CQRS.Library.BorrowingService.Bootstraping;
public static class ApplicationServiceExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();

        // Add EF Core
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<BorrowingDbContext>(options =>
        options.UseNpgsql(connectionString));

        return builder;
    }
}
