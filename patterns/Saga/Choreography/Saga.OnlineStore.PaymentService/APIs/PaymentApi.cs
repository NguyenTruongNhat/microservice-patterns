using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.IntegrationEvents;
using Saga.OnlineStore.PaymentService.Infrastructure.Entity;

namespace Saga.OnlineStore.PaymentService.APIs
{

    public static class PaymentApiExtensions
    {
        public static IEndpointRouteBuilder MapPaymentApi(this IEndpointRouteBuilder builder)
        {
            builder.MapGroup("/api/saga/v1")
                  .MapPaymentApi()
                  .WithTags("Payment Api");
            return builder;
        }
        public static RouteGroupBuilder MapPaymentApi(this RouteGroupBuilder group)
        {
            group.MapGet("cards", async ([AsParameters] ApiServices services) =>
            {
                return await services.DbContext.Cards.ToListAsync();
            });

            group.MapGet("cards/{id:guid}", async ([AsParameters] ApiServices services, Guid id) =>
            {
                return await services.DbContext.Cards.FindAsync(id);
            });

            group.MapPost("cards", PaymentApi.CreateCard);

            group.MapDelete("cards/{id:guid}", PaymentApi.DeleteCard);
            group.MapPut("cards/{id:guid}/deposit", PaymentApi.Deposit);
            return group;
        }


    }

    public class PaymentApi
    {
        public static async Task<Results<Ok<Card>, BadRequest>> CreateCard([AsParameters] ApiServices services, Card card)
        {
            if (card == null)
            {
                return TypedResults.BadRequest();
            }

            if (card.Balance != 0)
            {
                services.Logger.LogWarning("Creating a card with non-zero balance is not allowed.");
                return TypedResults.BadRequest();
            }

            if (card.Id == Guid.Empty) card.Id = Guid.NewGuid();

            var existingCard = await services.DbContext.Cards.Where(c => c.CardNumber == card.CardNumber).SingleOrDefaultAsync();
            if (existingCard != null)
            {
                services.Logger.LogError("Card already exists");
                return TypedResults.BadRequest();
            }

            await services.DbContext.Cards.AddAsync(card);
            await services.DbContext.SaveChangesAsync();

            await services.EventPublisher.PublishAsync(new CardCreatedIntegrationEvent()
            {
                CardId = card.Id,
                CardNumber = card.CardNumber,
                ExpirationDate = card.ExpirationDate,
                CardHolderName = card.CardHolderName,
                Cvv = card.Cvv
            });

            return TypedResults.Ok(card);
        }
        public static async Task<Results<NotFound, Ok>> DeleteCard([AsParameters] ApiServices services, Guid id)
        {
            var r = await services.DbContext.Cards.Where(c => c.Id == id).ExecuteDeleteAsync();
            if (r == 0)
            {
                return TypedResults.NotFound();
            }
            await services.EventPublisher.PublishAsync(new CardDeletedIntegrationEvent()
            {
                CardId = id
            });
            return TypedResults.Ok();
        }

        public static async Task<Results<NotFound, Ok, BadRequest>> Deposit([AsParameters] ApiServices services, Guid id, [FromBody] Deposit deposit)
        {
            if (deposit.Amount <= 0)
            {
                services.Logger.LogWarning("Deposit amount must be greater than zero.");
                return TypedResults.BadRequest();
            }

            var card = await services.DbContext.Cards.Where(c => c.Id == id).SingleOrDefaultAsync();
            if (card == null)
            {
                return TypedResults.NotFound();
            }

            card.Balance += deposit.Amount;
            services.DbContext.Cards.Update(card);

            await services.DbContext.SaveChangesAsync();

            await services.EventPublisher.PublishAsync(new CardBalanceChangedIntegrationEvent()
            {
                CardId = id,
                Balance = card.Balance
            });

            return TypedResults.Ok();

        }




    }

    public record Deposit
    {
        public decimal Amount { get; set; }
    }

}
