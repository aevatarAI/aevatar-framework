namespace Aevatar.Core.Abstractions.Exceptions;

public class StateTransitionException(string message, Exception inner) : Exception(message, inner);
