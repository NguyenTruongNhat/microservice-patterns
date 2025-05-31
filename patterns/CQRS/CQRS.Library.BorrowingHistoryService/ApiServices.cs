using CQRS.Library.BorrowingHistoryService.Infrastructure.Data;

namespace CQRS.Library.BorrowingHistoryService;
public class ApiServices(
    BorrowingHistoryDbContext dbContext)
{
    public BorrowingHistoryDbContext DbContext => dbContext;

}
