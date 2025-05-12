# Aevatar.Core Module Documentation

## Data Flow Sequence Diagram

```mermaid
sequenceDiagram
    participant Client
    participant GAgentFactory
    participant GAgentManager
    participant GAgentBase
    participant EventDispatcher
    participant StateProjection
    
    Client->>GAgentFactory: CreateGAgent()
    GAgentFactory->>GAgentManager: Register(agent)
    GAgentManager->>GAgentBase: Initialize()
    GAgentBase->>EventDispatcher: Subscribe()
    
    Client->>GAgentBase: PublishEvent(event)
    GAgentBase->>EventDispatcher: Dispatch(event)
    EventDispatcher->>GAgentBase: OnEvent(event)
    GAgentBase->>StateProjection: ApplyEvent(event)
    StateProjection-->>GAgentBase: Updated State
    GAgentBase-->>Client: Event Published Response
```

## Relationship Diagram

```mermaid
classDiagram
    class IGAgentFactory {
        +CreateGAgent<T>() IGAgent<T>
    }
    
    class IGAgentManager {
        +Register(agent) void
        +Unregister(agent) void
        +GetAgent(id) IGAgent
    }
    
    class GAgentBase {
        +Id string
        +Publish(event) Task
        +Subscribe() void
        +HandleEvent(event) Task
    }
    
    class IEventDispatcher {
        +Dispatch(event) Task
        +Subscribe(handler) void
    }
    
    class StateProjection {
        +Apply(event) void
        +GetState() TState
    }
    
    IGAgentFactory ..> GAgentBase : creates
    IGAgentManager --> GAgentBase : manages
    GAgentBase --> IEventDispatcher : uses
    GAgentBase --> StateProjection : uses
    ArtifactGAgent --|> GAgentBase : extends
    StateProjectionGAgentBase --|> GAgentBase : extends
```

## Module Explanation

The Aevatar.Core module serves as the foundation of the Aevatar Framework, implementing the Generative Agent (GAgent) concept. GAgents are the primary abstraction for intelligent, stateful, event-driven entities that can respond to and publish events.

Key components include:
- **GAgentBase**: The core implementation providing event publishing, subscription, and handling capabilities
- **GAgentFactory**: Creates instances of GAgents with appropriate state types
- **GAgentManager**: Manages agent lifecycle, registration, and retrieval
- **StateProjection**: Handles state updates based on incoming events
- **EventDispatcher**: Routes events between agents based on subscriptions

The module implements a lightweight event-sourcing pattern where agent state changes are driven by events. These events can trigger reactions in other agents through the subscription mechanism, creating a network of communicating agents. The module supports specialized agent types like ArtifactGAgent for artifact-based operations and StateProjectionGAgentBase for advanced state management. 