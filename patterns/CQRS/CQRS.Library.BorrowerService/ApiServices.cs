using CQRS.Library.BorrowerService.Infrastructure.Data;

namespace CQRS.Library.BorrowerService;
public class ApiServices(BorrowerDbContext dbContext)
{
    public BorrowerDbContext DbContext => dbContext;
}
