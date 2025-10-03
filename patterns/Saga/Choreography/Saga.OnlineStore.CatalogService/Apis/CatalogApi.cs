using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Saga.OnlineStore.CatalogService.Infrastructure.Entity;
using Saga.OnlineStore.IntegrationEvents;

namespace Saga.OnlineStore.CatalogService.Apis
{
    public static class CatalogApi
    {
        public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/saga/v1")
                           .MapCatalogApi()
                           .WithTags("Catalog Service");

            return app;
        }

        public static RouteGroupBuilder MapCatalogApi(this RouteGroupBuilder group)
        {
            group.MapGet("/products", async ([AsParameters] ApiServices services) =>
            {
                return await services.DbContext.Products.ToListAsync();
            });

            group.MapGet("/products/{id:guid}", async ([AsParameters] ApiServices services, Guid id) =>
            {
                return await services.DbContext.Products.FindAsync(id);
            });

            group.MapPost("/products", CreateProduct);

            group.MapPut("/products/{id:guid}", UpdateProduct);


            return group;
        }

        private static async Task<Results<Ok<Product>, BadRequest>> CreateProduct([AsParameters] ApiServices service, Product product)
        {
            if (product == null)
            {
                return TypedResults.BadRequest();
            }
            if (product.Id == Guid.Empty)
                product.Id = Guid.NewGuid();

            await service.DbContext.Products.AddAsync(product);
            await service.DbContext.SaveChangesAsync();

            await service.EventPublisher.PublishAsync(new ProductCreatedIntegrationEvent()
            {
                ProductId = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price
            });

            return TypedResults.Ok(product);
        }

        private static async Task<Results<Ok<Product>, BadRequest>> UpdateProduct([AsParameters] ApiServices service,Guid id, Product product)
        {
            var existingProduct = await service.DbContext.Products.FindAsync(id);
            if (existingProduct == null || product == null)
            {
                return TypedResults.BadRequest();
            }
            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            await service.DbContext.SaveChangesAsync();

            await service.EventPublisher.PublishAsync(new ProductUpdatedIntegrationEvent()
            {
                ProductId = existingProduct.Id,
                Name = existingProduct.Name,
                Description = existingProduct.Description,
                Price = existingProduct.Price
            });

            return TypedResults.Ok(existingProduct);
        }
    }
}
