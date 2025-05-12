# Aevatar Framework Directory Structure

## Overview
The Aevatar Framework is organized with a clear separation of concerns, dividing the codebase into source code, samples, and tests.

## Root Directory
```
/
├── .git/                    # Git repository data
├── .github/                 # GitHub workflows and configuration
├── .idea/                   # JetBrains IDE configuration
├── samples/                 # Sample applications demonstrating framework usage
├── src/                     # Source code for the framework
├── test/                    # Test projects
├── TestResults/             # Test result output
├── PluginGAgent/            # Plugin for Agent functionality
├── aevatar-framework.sln    # Visual Studio solution file
├── Directory.Packages.props # Package references for the solution
├── Directory.Build.props    # Common build properties
├── common.props             # Additional common properties
├── LICENSE                  # License information
├── README.md                # Project documentation
└── .gitignore               # Git ignore configuration
```

## Source Code (`src/`)
```
src/
├── Aevatar/                        # Main framework implementation
│   └── Extensions/                 # Framework extension methods with Orleans host extensions
│
├── Aevatar.Core/                   # Core functionality
│   ├── Extensions/                 # Core extension methods
│   ├── Projections/                # Projection functionality for state management
│   ├── GAgentBase.cs               # Base implementation for Generative Agents
│   ├── GAgentBase.Publish.cs       # Publishing functionality for Generative Agents
│   ├── GAgentBase.Subscribe.cs     # Subscription functionality for Generative Agents
│   ├── GAgentBase.Observers.cs     # Observer functionality for Generative Agents
│   ├── GAgentBase.SyncWorker.cs    # Synchronization worker functionality
│   ├── GAgentFactory.cs            # Factory for creating Generative Agents
│   ├── GAgentManager.cs            # Management of Generative Agent lifecycle
│   ├── ArtifactGAgent.cs           # Artifact-based Generative Agents
│   └── StateProjectionGAgentBase.cs # Base for state projection agents
│
├── Aevatar.Core.Abstractions/      # Core interfaces and abstractions
│   ├── Application/                # Application layer abstractions
│   ├── Events/                     # Event definitions and interfaces
│   ├── Exceptions/                 # Framework exceptions
│   ├── Extensions/                 # Extension interfaces
│   ├── Infrastructure/             # Infrastructure abstractions
│   ├── Plugin/                     # Plugin system interfaces
│   ├── Projections/                # Projection interfaces
│   ├── SyncWorker/                 # Synchronization worker interfaces
│   ├── IEventDispatcher.cs         # Interface for event dispatching
│   ├── IGAgentFactory.cs           # Interface for Generative Agent factory
│   ├── IGAgentManager.cs           # Interface for Generative Agent management
│   └── IStateAgent.cs              # Interface for state-based agents
│
├── Aevatar.EventSourcing.Core/     # Event sourcing core functionality
│   ├── Exceptions/                 # Event sourcing exceptions
│   ├── Hosting/                    # Hosting configuration for event sourcing
│   ├── LogConsistency/             # Log consistency implementation
│   ├── Snapshot/                   # Snapshot functionality
│   └── Storage/                    # Storage implementations for event sourcing
│
├── Aevatar.EventSourcing.MongoDB/  # MongoDB implementation for event sourcing
│
├── Aevatar.PermissionManagement/   # Permission management functionality
│
└── Aevatar.Plugins/                # Plugin system implementation
```

## Samples (`samples/`)
```
samples/
├── ArtifactGAgent/              # Sample for artifact generative agents
│
├── MessagingGAgent.Client/      # Client for messaging generative agents
│
├── MessagingGAgent.Grains/      # Grain implementations for messaging
│
├── MessagingGAgent.Silo/        # Silo host for messaging agents
│
├── PluginGAgent/                # Sample for plugin-based generative agents
│
├── PubSubDemoWithoutGroup/      # Pub/Sub demo without grouping
│
├── SimpleAIGAgent/              # Simple AI generative agent demo
│   ├── SimpleAIGAgent.Client/   # Client for simple AI agents
│   ├── SimpleAIGAgent.Grains/   # Grain implementations for AI agents
│   └── SimpleAIGAgent.Silo/     # Silo host for simple AI agents
│
└── TokenUsageProjection/        # Sample for token usage projection
```

## Tests (`test/`)
```
test/
├── Aevatar.Core.Tests/                  # Tests for core functionality
│   ├── TestArtifacts/                   # Test artifacts for testing
│   ├── TestEvents/                      # Test event implementations
│   ├── TestGAgents/                     # Test generative agent implementations
│   ├── TestGEvents/                     # Test generative event implementations
│   ├── TestInitializeDtos/              # Test initialization DTOs
│   ├── TestStates/                      # Test state implementations
│   ├── GroupingTests.cs                 # Tests for agent grouping
│   ├── PublishingTests.cs               # Tests for event publishing
│   ├── EventHandlingTests.cs            # Tests for event handling
│   └── EventSourcingTests.cs            # Tests for event sourcing
│
├── Aevatar.EventSourcing.Core.Tests/    # Tests for event sourcing core
│
├── Aevatar.EventSourcing.MongoDB.Tests/ # Tests for MongoDB implementation
│
├── Aevatar.GAgents.Plugins/             # Plugin tests for generative agents
│
├── Aevatar.GAgents.Tests/               # Tests for generative agents
│
├── Aevatar.PermissionManagement.Tests/  # Tests for permission management
│
├── Aevatar.Plugins.Tests/               # Tests for plugin system
│
├── Aevatar.TestBase/                    # Common test utilities and base classes
│
├── Aevatar.Tests/                       # General framework tests
│
├── OrleansTestKit/                      # Orleans test utilities
│
└── OrleansTestKit.Tests/                # Tests for Orleans test utilities
```

## Project Structure
The Aevatar Framework is built on .NET, using:
- Event sourcing architecture with MongoDB integration
- Orleans for distributed computing and actor model
- Plugin-based extensibility system
- Generative agents (GAgents) for AI integration

The solution follows a modular approach with clear separation between core components, plugins, and specialized implementations. Sample applications demonstrate various use cases, while extensive test coverage ensures reliability.

### Key Components
- **Generative Agents (GAgents)**: Core abstraction for AI-powered agents
- **Event Sourcing**: Persistence mechanism based on event streams
- **Plugin System**: Extensibility through pluggable components
- **Orleans Integration**: Actor model implementation using Microsoft Orleans
- **State Projections**: State management through event projections 