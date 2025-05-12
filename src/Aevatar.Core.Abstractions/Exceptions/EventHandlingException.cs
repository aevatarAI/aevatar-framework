namespace Aevatar.Core.Abstractions.Exceptions;

public class EventHandlingException(string message, Exception ex) : Exception(message, ex);