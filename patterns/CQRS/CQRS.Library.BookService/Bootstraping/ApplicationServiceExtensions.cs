﻿using EventBus;
using EventBus.Abstractions;

namespace CQRS.Library.BookService.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.AddServiceDefaults();
            builder.Services.AddOpenApi();
            builder.AddNpgsqlDbContext<BookDbContext>(EventBus.Consts.DefaultDatabase);

            builder.AddKafkaProducer("kafka");
            var kafkaTopic = builder.Configuration.GetValue<string>(EventBus.Consts.Env_EventPublishingTopics);
            if (!string.IsNullOrEmpty(kafkaTopic))
            {
                builder.AddKafkaEventPublisher(kafkaTopic);
            }
            else
            {
                builder.Services.AddTransient<IEventPublisher, NullEventPublisher>();
            }

            return builder;
        }
    }
}
