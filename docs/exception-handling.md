# Exception Capturing and Publishing

This document describes how to use the exception capturing and publishing features of the Aevatar framework. This functionality allows exception information to be written to Orleans Stream for centralized processing, monitoring, and analysis.

## Feature Overview

The exception capturing and publishing functionality sends exception information to a dedicated exception handling channel via Orleans Stream. Key features include:

- Publishing detailed exception information to a dedicated Orleans Stream
- Supporting the recording of exception context information
- Automatically capturing calling method and class name
- Providing easy-to-use helper methods to simplify the exception handling process
- Using a separate Kafka Topic, isolated from business Topics

## Configuration

Add the following configuration to the `appsettings.json` file:

```json
{
  "Aevatar": {
    "ExceptionStreamNamespace": "AevatarException"
  }
}
```

Where `ExceptionStreamNamespace` specifies the Stream namespace for exception events, with a default value of "AevatarException".

## Usage

### Direct Exception Publishing

```csharp
public class MyGAgent : GAgentBase<MyState, MyStateLogEvent>
{
    public async Task DoSomethingAsync()
    {
        try
        {
            // Business logic
            await ProcessDataAsync();
        }
        catch (Exception ex)
        {
            // Publish exception
            var contextData = new { UserId = "user123", Action = "ProcessData" };
            await this.PublishExceptionAsync(ex, contextData);
            
            // Can choose to rethrow or handle the exception
            throw;
        }
    }
}
```

### Using Helper Methods to Automatically Capture and Publish Exceptions

```csharp
public class MyGAgent : GAgentBase<MyState, MyStateLogEvent>
{
    public async Task DoSomethingAsync()
    {
        var contextData = new { UserId = "user123", Action = "ProcessData" };
        
        // Automatically capture and publish exceptions, rethrown by default
        await this.CatchAndPublishExceptionAsync(async () =>
        {
            await ProcessDataAsync();
        }, contextData);
    }
    
    public async Task<Result> GetDataAsync()
    {
        var contextData = new { UserId = "user123", Action = "GetData" };
        
        // For cases with return values, without rethrowing the exception
        var (result, exceptionId) = await this.CatchAndPublishExceptionAsync(
            async () => await FetchDataAsync(),
            new Result { Success = false },  // Default value
            contextData,
            rethrowException: false);
            
        if (exceptionId != Guid.Empty)
        {
            // Exception occurred, using default value
            Logger.LogWarning("Exception occurred, using default value. ExceptionId: {ExceptionId}", exceptionId);
        }
        
        return result;
    }
}
```

## Exception Event Format

The published exception event contains the following information:

```csharp
public class ExceptionEvent : EventBase
{
    public GrainId GrainId { get; set; }           // Grain ID where the exception occurred
    public string ExceptionMessage { get; set; }   // Exception message
    public string ExceptionType { get; set; }      // Exception type
    public string StackTrace { get; set; }         // Stack trace
    public string ContextData { get; set; }        // Context data (JSON format)
    public DateTime Timestamp { get; set; }        // Exception timestamp (UTC)
    public string? MethodName { get; set; }        // Method name where the exception occurred
    public string? ClassName { get; set; }         // Class name where the exception occurred
}
```

## Exception Handling Process

1. Exception is captured in the GAgent
2. Exception information is encapsulated as an ExceptionEvent
3. ExceptionEvent is published to a dedicated Orleans Stream
4. Via Kafka, exception events are routed to consumers for processing
5. Exception handling services can aggregate, analyze, and alert on exceptions

## Best Practices

- Add exception capturing and publishing for important or complex operations
- Include sufficient information in the context data to facilitate troubleshooting
- When handling sensitive data, be careful not to include personal privacy information in the context
- For high-frequency operations, consider setting an exception sampling rate to avoid too many exception events affecting performance
- Implement exception consumer services for real-time monitoring and analysis of exceptions 