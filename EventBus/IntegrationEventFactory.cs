using EventBus.Events;
using System.Reflection;
using System.Text.Json;

namespace EventBus
{
    public class IntegrationEventFactory : IIntegrationEventFactory
    {
        public IntegrationEvent? CreateEvent(string typeName, string value)
        {
            var t = GetEventType(typeName) ?? throw new ArgumentException($"Type {typeName} not found");
            return JsonSerializer.Deserialize(value, t) as IntegrationEvent;
        }

        private static Type? GetEventType(string type)
        {
            var t = Type.GetType(type);
            return t ?? AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == type);
        }

        public static readonly IntegrationEventFactory Instance = new();
    }

    public class IntegrationEventFactory<TEvent> : IIntegrationEventFactory
    {
        private static readonly Assembly integrationEventAssembly = typeof(TEvent).Assembly;

        public IntegrationEvent? CreateEvent(string typeName, string value)
        {
            var t = GetEventType(typeName) ?? throw new ArgumentException($"Type {typeName} not found");
            return JsonSerializer.Deserialize(value, t) as IntegrationEvent;
        }

        private static Type? GetEventType(string type)
        {
            var t = integrationEventAssembly.GetType(type) ?? Type.GetType(type);

            return t ?? AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == type);
        }

        public static readonly IntegrationEventFactory<TEvent> Instance = new();
    }

}
