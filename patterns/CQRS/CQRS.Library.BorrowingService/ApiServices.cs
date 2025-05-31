using CQRS.Library.BorrowingService.Infrastructure.Data;

namespace CQRS.Library.BorrowingService;
public class ApiServices(BorrowingDbContext dbContext)
{
    public BorrowingDbContext DbContext => dbContext;

}
