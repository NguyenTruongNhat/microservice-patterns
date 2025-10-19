using EventBus.Abstractions;
using Saga.OnlineStore.PaymentService.APIs;
using Saga.OnlineStore.PaymentService.Infrastructure.Data;

namespace Saga.OnlineStore.PaymentService;
public class ApiServices(
    PaymentDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<PaymentApi> logger)
{
    public PaymentDbContext DbContext => dbContext;
    public IEventPublisher EventPublisher => eventPublisher;
    public ILogger<PaymentApi> Logger => logger;
}
