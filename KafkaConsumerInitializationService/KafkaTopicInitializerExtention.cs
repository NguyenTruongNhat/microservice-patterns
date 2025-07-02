using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KafkaConsumerInitializationService
{
    public static class KafkaTopicInitializerExtention
    {
        public static IHostApplicationBuilder KafkaTopicInitializer(this IHostApplicationBuilder builder, Action<KafkaTopicInitializerOptions>? configureOptions = null)
        {
            var options = new KafkaTopicInitializerOptions();
            configureOptions?.Invoke(options);
            builder.Services.AddSingleton(options);
            builder.Services.AddHostedService<KafkaTopicInitializerHostedService>();
            return builder;
        }
    }
}
    
