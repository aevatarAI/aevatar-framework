# Background
  (1) The project is developed using C# and uses the Orleans technology stack. GAgent is a wrapper for Orleans Grain.
  (2) It is necessary to capture exceptions in implemented GAgent EventHandlers and notify subscribers of the exception information and corresponding context information through Orleans Stream.
  (3) Subscribers do not need to handle exceptions through GAgent's EventHandle, but instead assemble their own StreamProvider and subscribe to the stream.

# Reference Files
  • README.md
  • DIRECTORY_STRUCTURE.md
  • Aevatar.Core.md

# Features
  Implement a method in the Aevatar.Core project to write exceptions to Orleans Stream, with the following capabilities:
  a. Must include exception information, context of the exception, and information such as the time the exception was thrown
  b. Requires a separate Kafka Topic to handle exceptions, isolated from business topics, with the exception topic name read from configuration

# Note
  a. Be sure not to modify existing designs and implementations.