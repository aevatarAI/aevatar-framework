using System;
using System.Threading.Tasks;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Samples.ExceptionHandling;

/// <summary>
/// Sample State Class
/// </summary>
[GenerateSerializer]
public class SampleState : StateBase
{
    [Id(0)] public int Counter { get; set; }
}

/// <summary>
/// Sample State Log Event Class
/// </summary>
[GenerateSerializer]
public class SampleStateLogEvent : StateLogEventBase<SampleStateLogEvent>
{
}

/// <summary>
/// Sample GAgent demonstrating how to use exception capturing and publishing features
/// </summary>
[GAgent]
public class ExceptionHandlingSampleGAgent : GAgentBase<SampleState, SampleStateLogEvent>
{
    private readonly ILogger<ExceptionHandlingSampleGAgent> _logger;

    public ExceptionHandlingSampleGAgent(ILogger<ExceptionHandlingSampleGAgent> logger) : base(logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Demonstrates how to use exception publishing feature directly
    /// </summary>
    public async Task DemoDirectExceptionPublishingAsync()
    {
        _logger.LogInformation("Starting direct exception publishing demo");
        
        try
        {
            // Simulate an exception
            throw new InvalidOperationException("This is a test exception");
        }
        catch (Exception ex)
        {
            // Create context data
            var contextData = new
            {
                Operation = "DemoDirectExceptionPublishing",
                Timestamp = DateTime.UtcNow,
                GrainId = this.GetGrainId().ToString()
            };
            
            // Publish exception directly
            var exceptionId = await this.PublishExceptionAsync(ex, contextData);
            
            _logger.LogInformation("Published exception with ID: {ExceptionId}", exceptionId);
            
            // In real applications, you might choose to rethrow or handle the exception
            // throw;
        }
        
        _logger.LogInformation("Completed direct exception publishing demo");
    }
    
    /// <summary>
    /// Demonstrates how to use helper method to catch and publish exceptions (without return value)
    /// </summary>
    public async Task DemoExceptionHandlingWithoutResultAsync()
    {
        _logger.LogInformation("Starting exception handling demo without result");
        
        var contextData = new
        {
            Operation = "DemoExceptionHandlingWithoutResult",
            Timestamp = DateTime.UtcNow,
            GrainId = this.GetGrainId().ToString()
        };
        
        // Use helper method to catch and publish exceptions, without rethrowing the exception
        var exceptionId = await this.CatchAndPublishExceptionAsync(
            async () =>
            {
                // Simulate an exception
                await Task.Delay(100);
                throw new ArgumentException("Invalid argument in operation");
            },
            contextData,
            rethrowException: false);
        
        if (exceptionId != Guid.Empty)
        {
            _logger.LogInformation("Exception occurred and published with ID: {ExceptionId}", exceptionId);
        }
        
        _logger.LogInformation("Completed exception handling demo without result");
    }
    
    /// <summary>
    /// Demonstrates how to use helper method to catch and publish exceptions (with return value)
    /// </summary>
    public async Task<(bool Success, string Message)> DemoExceptionHandlingWithResultAsync()
    {
        _logger.LogInformation("Starting exception handling demo with result");
        
        var contextData = new
        {
            Operation = "DemoExceptionHandlingWithResult",
            Timestamp = DateTime.UtcNow,
            Parameters = new { Id = "sample-id", RequestType = "GET" }
        };
        
        // Use helper method to catch and publish exceptions, with return value, without rethrowing the exception
        var (result, exceptionId) = await this.CatchAndPublishExceptionAsync(
            async () =>
            {
                // Simulate a successful operation
                await Task.Delay(100);
                
                // May throw an exception based on conditions
                if (DateTime.UtcNow.Millisecond % 2 == 0)
                {
                    throw new TimeoutException("Operation timed out");
                }
                
                return (Success: true, Message: "Operation completed successfully");
            },
            (Success: false, Message: "Operation failed due to an exception"),  // Default value
            contextData,
            rethrowException: false);
        
        if (exceptionId != Guid.Empty)
        {
            _logger.LogInformation("Exception occurred and published with ID: {ExceptionId}", exceptionId);
            _logger.LogInformation("Using default result: {Result}", result);
        }
        else
        {
            _logger.LogInformation("Operation completed successfully: {Result}", result);
        }
        
        _logger.LogInformation("Completed exception handling demo with result");
        
        return result;
    }
    
    /// <summary>
    /// Demonstrates how to use exception capturing and publishing in actual business logic
    /// </summary>
    public async Task<int> PerformBusinessOperationAsync(int value)
    {
        _logger.LogInformation("Performing business operation with value: {Value}", value);
        
        // Catch and publish exceptions with business context data
        var contextData = new 
        { 
            OperationName = "PerformBusinessOperation", 
            InputValue = value 
        };
        
        var (result, exceptionId) = await this.CatchAndPublishExceptionAsync(
            async () =>
            {
                // Simulate business logic
                await Task.Delay(100);
                
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Value cannot be negative");
                }
                
                // Update state
                RaiseEvent(new SampleStateLogEvent());
                State.Counter += value;
                
                return State.Counter;
            },
            -1,  // Default value, indicating operation failure
            contextData,
            rethrowException: false);
        
        if (exceptionId != Guid.Empty)
        {
            _logger.LogWarning("Business operation failed with exception ID: {ExceptionId}", exceptionId);
        }
        else
        {
            _logger.LogInformation("Business operation completed successfully, new counter value: {Counter}", result);
        }
        
        return result;
    }
    
    /// <summary>
    /// GAgent description
    /// </summary>
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("Exception Handling Sample GAgent");
    }
} 