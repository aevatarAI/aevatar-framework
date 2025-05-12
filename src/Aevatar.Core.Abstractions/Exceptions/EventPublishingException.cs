namespace Aevatar.Core.Abstractions.Exceptions;

public class EventPublishingException(string message, Exception ex) : Exception(message, ex);
