using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Saga.TripPlanner.TicketService.Infrastructure.Entity;

namespace Saga.TripPlanner.TicketService.Apis
{
    public static class TicketApiExtentions
    {
        public static IEndpointRouteBuilder MapTicketApi(this IEndpointRouteBuilder builder)
        {
            builder.MapGroup("/api/saga/v1")
              .MapTicketApi()
              .WithTags("Ticket api");

            return builder;
        }

        public static RouteGroupBuilder MapTicketApi(this RouteGroupBuilder group)
        {
            group.MapGet("tickets-types", async ([AsParameters] ApiServices service) =>
            {
                return await service.DbContext.TicketTypes.ToListAsync();
            });

            group.MapGet("tickets-types/{id}", async ([AsParameters] ApiServices service, string id) =>
            {
                return await service.DbContext.TicketTypes.Where(tt => tt.Id == id).FirstOrDefaultAsync();
            });

            group.MapGet("tickets", async ([AsParameters] ApiServices service) =>
            {
                return await service.DbContext.Tickets.ToListAsync();
            });

            group.MapGet("tickets/{id}", async ([AsParameters] ApiServices service, Guid id) =>
            {
                return await service.DbContext.Tickets.Where(t => t.Id == id).FirstOrDefaultAsync();
            });

            group.MapPost("ticket-type", TicketApi.CreateTicketType);
            group.MapPost("tickets", TicketApi.BookTickets);
            group.MapPut("tickets/cancel", TicketApi.CancelTickets);

            return group;
        }

        public class TicketApi
        {
            internal static async Task<Results<Ok<List<Ticket>>, BadRequest>> BookTickets([AsParameters] ApiServices services, List<Ticket> tickets)
            {
                if (tickets == null || tickets.Count == 0)
                {
                    return TypedResults.BadRequest();
                }
                foreach (Ticket ticket in tickets)
                {
                    var ticketType = await services.DbContext.TicketTypes.FindAsync(ticket.TicketTypeId);
                    if (ticketType == null)
                    {
                        services.Logger.LogWarning("Ticket type {TicketTypeId} not found", ticket.TicketTypeId);
                        return TypedResults.BadRequest();
                    }
                    if (ticketType.AvailableTickets <= 0)
                    {
                        services.Logger.LogWarning("No available tickets for ticket type {TicketTypeId}", ticket.TicketTypeId);
                        return TypedResults.BadRequest();
                    }

                    if (ticket.Id == Guid.Empty)
                    {
                        ticket.Id = Guid.NewGuid();
                    }

                    ticket.Price = ticketType.Price;
                    ticket.Status = TicketStatus.Booked;
                    ticketType.AvailableTickets -= 1;

                    services.DbContext.Tickets.Add(ticket);
                }
                await services.DbContext.SaveChangesAsync();

                return TypedResults.Ok(tickets);
            }

            internal static async Task<Results<Ok, BadRequest, NotFound>> CancelTickets([AsParameters] ApiServices services, List<Guid> ticketIds)

            {
                if (ticketIds == null || ticketIds.Count == 0)
                {
                    return TypedResults.BadRequest();
                }

                foreach (Guid id in ticketIds)
                {
                    var ticket = await services.DbContext.Tickets.FindAsync(id);
                    if (ticket == null)
                    {
                        return TypedResults.NotFound();
                    }

                    if (ticket.Status != TicketStatus.Booked)
                    {
                        services.Logger.LogInformation("Ticket {id} is not in Booked state", ticket.Id);
                        return TypedResults.BadRequest();
                    }

                    ticket.Status = TicketStatus.Cancelled;
                    ticket.TicketType.AvailableTickets++;
                }

                await services.DbContext.SaveChangesAsync();
                services.Logger.LogInformation("Ticket {ids} confirmed successfully", ticketIds);

                return TypedResults.Ok();
            }

            internal static async Task<Results<Ok<TicketType>, BadRequest, Conflict>> CreateTicketType([AsParameters] ApiServices services, TicketType tickeType)

            {
                if (tickeType == null)
                {
                    return TypedResults.BadRequest();
                }

                if (string.IsNullOrWhiteSpace(tickeType.Id))
                {
                    services.Logger.LogInformation("Ticket type id is required");
                    return TypedResults.BadRequest();
                }

                if (string.IsNullOrWhiteSpace(tickeType.Name))
                {
                    services.Logger.LogInformation("Ticket type name is required");
                    return TypedResults.BadRequest();
                }

                if (tickeType.Price <= 0)
                {
                    services.Logger.LogInformation("Ticket type price must be greater than 0");
                    return TypedResults.BadRequest();
                }

                if (tickeType.AvailableTickets < 0)
                {
                    services.Logger.LogInformation("Ticket type available tickets must be greater than or equal to 0");
                    return TypedResults.BadRequest();
                }

                if (await services.DbContext.TicketTypes.AnyAsync(x => x.Id == tickeType.Id))
                {
                    services.Logger.LogInformation("Ticket type {id} already exists", tickeType.Id);
                    return TypedResults.Conflict();
                }

                await services.DbContext.TicketTypes.AddAsync(tickeType);
                await services.DbContext.SaveChangesAsync();

                return TypedResults.Ok(tickeType);
            }
        }
    }


}
