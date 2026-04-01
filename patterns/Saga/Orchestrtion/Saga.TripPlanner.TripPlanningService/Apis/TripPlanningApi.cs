using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Saga.TripPlanner.HotelService.Infrastructure.Entity;
using Saga.TripPlanner.PaymentService.Apis;
using Saga.TripPlanner.TicketService.Infrastructure.Entity;
using Saga.TripPlanner.TripPlanningService.Infrastructure.Entity;
using System.Threading;

namespace Saga.TripPlanner.TripPlanningService.Apis
{
    public static class TripPlanningApiExtensions
    {
        public static IEndpointRouteBuilder MapTripPlanningApi(this IEndpointRouteBuilder builder)
        {
            builder.MapGroup("/api/saga/v1")
                  .MapTripPlanningApi()
                  .WithTags("Trip Planning Api");

            return builder;
        }

        public static RouteGroupBuilder MapTripPlanningApi(this RouteGroupBuilder group)
        {
            group.MapGet("trips", async ([AsParameters] ApiServices services) =>
            {
                return await services.DbContext.Trips.ToListAsync();
            });

            group.MapGet("trips/{id:guid}", async ([AsParameters] ApiServices services, Guid id) =>
            {
                return await services.DbContext.Trips.FindAsync(id);
            });

            group.MapPost("trips", TripPlanningApi.CreateTrip);

            return group;
        }
    }

    public class TripPlanningApi
    {
        internal static async Task<Results<Ok<Trip>, BadRequest>> CreateTrip([AsParameters] ApiServices services, SagaServices sagaServices, Trip trip)
        {
            if (trip == null)
            {
                return TypedResults.BadRequest();
            }

            if (trip.Id == Guid.Empty)
            {
                trip.Id = Guid.CreateVersion7();
            }
            trip.CreationDate = DateTime.UtcNow;

            await services.DbContext.Trips.AddAsync(trip);

            await HandleSaga(services, sagaServices, trip);

            // You can publish TripCreatedIntegrationEvent

            return TypedResults.Ok(trip);
        }

        private static async Task HandleSaga(ApiServices services, SagaServices sagaServices, Trip trip, CancellationToken cancellationToken = default)
        {
            // it is better to offload this Saga handing part to an async service, but I don't want to make this sample too complicated
            int retryCount = 3;
            while (retryCount-- > 0 && trip.Status != TripStatus.Rejected && trip.Status != TripStatus.Confirmed)
            {
                if (trip.Status == TripStatus.Pending)
                {
                    var tickets = trip.TicketBookings.Select(tb => new Ticket()
                    {
                        Id = Guid.CreateVersion7(),
                        TicketTypeId = tb.TicketTypeId,
                    });
                    var ticketResponse = await sagaServices.TicketHttpClient.PostAsJsonAsync("/api/saga/v1/tickets", tickets, cancellationToken);
                    if (ticketResponse.IsSuccessStatusCode)
                    {
                        services.Logger.LogInformation("Tickets booked successfully for trip {TripId}", trip.Id);
                        trip.Status = TripStatus.TicketsBooked;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        services.Logger.LogError("Failed to book tickets for trip {TripId}", trip.Id);
                        trip.Status = TripStatus.Rejected;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (trip.Status == TripStatus.TicketsBooked)
                {
                    var hotelBookings = trip.HotelRoomBookings.Select(hb => new Booking()
                    {
                        RoomId = hb.RoomId,
                        TripId = trip.Id,
                        CheckInDate = hb.CheckInDate,
                        CheckOutDate = hb.CheckOutDate
                    });
                    var hotelResponse = await sagaServices.HotelHttpClient.PostAsJsonAsync("/api/saga/v1/hotelbookings", hotelBookings, cancellationToken);
                    if (hotelResponse.IsSuccessStatusCode)
                    {
                        services.Logger.LogInformation("Hotel rooms booked successfully for trip {TripId}", trip.Id);
                        trip.Status = TripStatus.HotelRoomsBooked;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        services.Logger.LogError("Failed to book hotel rooms for trip {TripId}", trip.Id);
                        trip.Status = TripStatus.HotelRoomBookingFailed;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (trip.Status == TripStatus.HotelRoomsBooked)
                {
                    var paymentRequest = new CreditCardPayment()
                    {
                        CardHolderName = trip.CardHolderName,
                        ExpirationDate = trip.ExpirationDate,
                        Cvv = trip.Cvv,
                        Amount = trip.Amount
                    };
                    var paymentResponse = await sagaServices.PaymentHttpClient.PutAsJsonAsync($"/api/saga/v1/cards/{trip.CardNumber}/pay", paymentRequest, cancellationToken);
                    if (paymentResponse.IsSuccessStatusCode)
                    {
                        services.Logger.LogInformation("Payment processed successfully for trip {TripId}", trip.Id);
                        trip.Status = TripStatus.Confirmed;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        services.Logger.LogError("Failed to process payment for trip {TripId}", trip.Id);
                        trip.Status = TripStatus.PaymentFailed;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (trip.Status == TripStatus.TicketsFailed)
                {
                    // in this code sample we don't implement compensation actions because I set the TripStatus to Rejected directly
                }
                else if (trip.Status == TripStatus.HotelRoomBookingFailed)
                {
                    // cancel tickets
                    services.Logger.LogInformation("[Compensating transaction] Cancelling tickets");

                    var ticketIds = trip.TicketBookings.Select(t => t.Id);
                    var ticketResponse = await sagaServices.TicketHttpClient.PutAsJsonAsync("/api/saga/v1/tickets/cancel",
                        ticketIds,
                        cancellationToken);
                    if (ticketResponse.IsSuccessStatusCode)
                    {
                        trip.Status = TripStatus.Rejected;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }

                    // if ticket cancellation fails, we need to retry in next loop
                }
                else if (trip.Status == TripStatus.PaymentFailed)
                {
                    // cancel hotel rooms
                    services.Logger.LogInformation("[Compensating transaction] Cancelling hotel rooms");

                    var roomBookingIds = trip.HotelRoomBookings.Select(r => r.Id);
                    var hotelRoomResponse = await sagaServices.HotelHttpClient.PutAsJsonAsync("/api/saga/v1/bookings", roomBookingIds, cancellationToken: cancellationToken);
                    if (hotelRoomResponse.IsSuccessStatusCode)
                    {
                        trip.Status = TripStatus.HotelRoomBookingFailed;
                        await services.DbContext.SaveChangesAsync(cancellationToken);
                    }

                    // if hotel room cancellation fails, we need to retry in next loop


                }
            }

            if (trip.Status != TripStatus.Confirmed)
            {
                Console.WriteLine("publish TripRejectedIntegration Event");
            }
            else
            {
                Console.WriteLine("publish TripBookedIntegration Event ");
            }

        }
    }
}
