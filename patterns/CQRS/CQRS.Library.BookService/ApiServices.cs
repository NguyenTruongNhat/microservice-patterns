using CQRS.Library.BookService.Infrastructure.Data;

namespace CQRS.Library.BookService;

public class ApiServices(BookDbContext dbContext)
{
    public BookDbContext DbContext => dbContext;
}
