namespace EventBus.Abstractions
{
    public class MessageEnvelop
    {
        public string MessageTypeName { get; set; } = default!;
        public string Message { get; set; } = default!;
        public MessageEnvelop() { }

        public MessageEnvelop(string messageTypeName, string message)
        {
            MessageTypeName = messageTypeName ?? throw new ArgumentNullException(nameof(messageTypeName));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public MessageEnvelop(Type type, string message) : this(type.FullName!, message)
        {

        }

    }
}
