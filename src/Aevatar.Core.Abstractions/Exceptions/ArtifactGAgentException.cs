namespace Aevatar.Core.Abstractions.Exceptions;

public class ArtifactGAgentException(string message, Exception inner) : Exception(message, inner);