# aevatar Framework

A distributed actor-based framework built on Microsoft Orleans for building scalable event-sourced applications.

## Overview

The GAgent Framework provides a base implementation for creating distributed agents (actors) that support:
- Event sourcing
- Pub/sub messaging
- State management
- Hierarchical agent relationships

## Core Components

### GAgentBase

The `GAgentBase<TState, TEvent>` class is the foundation of the framework, providing:

- **Event Sourcing**: Built on Orleans' `JournaledGrain` for reliable event storage and replay
- **State Management**: Manages agent state with automatic persistence
- **Event Publishing**: Supports publishing and subscribing to events between agents
- **Hierarchical Structure**: Allows agents to register with and subscribe to other agents

### Key Features

1. **Event Handling**
   - Automatic event forwarding
   - Custom event handler registration
   - Support for base and specialized event handlers

2. **State Management**
   - Strongly-typed state containers
   - Automatic state persistence
   - State change notifications

3. **Agent Registration**
   - Dynamic agent registration/unregistration
   - Hierarchical agent relationships
   - Subscription management

4. **Stream Processing**
   - Built-in stream provider integration
   - Automatic stream subscription management
   - Event forwarding capabilities

## Usage

### Creating a New Agent
```csharp
[GAgent]
public class MyAgent : GAgentBase<MyState, MyEvent>
{
    public MyAgent(ILogger logger) : base(logger)
    {
    }

    public override async Task<string> GetDescriptionAsync()
    {
        return "My Custom Agent";
    }
}
```

### Event Handling
```csharp
[EventHandler]
public async Task HandleCustomEventAsync(CustomEvent event)
{
    // Handle the event
}
```

### Agent Registration
```csharp
await agent.RegisterAsync(otherAgent);
```

## Best Practices

1. Always inherit from `GAgentBase` when creating new agents
1. Implement proper error handling in event handlers
1. Use strongly-typed events and states
1. Properly manage registrations

## Contributing

If you encounter a bug or have a feature request, please use the [Issue Tracker](https://github.com/AISmartProject/aevatar-framework/issues/new). The project is also open to contributions, so feel free to fork the project and open pull requests.

## License

Distributed under the Apache License. See [License](LICENSE) for more information.
Distributed under the MIT License. See [License](LICENSE) for more information.