using System.Reflection;
using AElf.OpenTelemetry.ExecutionTime;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TGEvent>
{
    [AggregateExecutionTime]
    private Task UpdateObserverList()
    {
        var eventHandlerMethods = GetEventHandlerMethods();

        foreach (var eventHandlerMethod in eventHandlerMethods)
        {
            var observer = new EventWrapperBaseAsyncObserver(async item =>
            {
                var grainId = (GrainId)item.GetType().GetProperty(nameof(EventWrapper<EventBase>.GrainId))?.GetValue(item)!;
                if (grainId == this.GetGrainId() && eventHandlerMethod.Name != nameof(ForwardEventAsync) &&
                    eventHandlerMethod.Name != AevatarGAgentConstants.InitializeDefaultMethodName)
                {
                    // Skip the event if it is sent by itself.
                    return;
                }

                var eventId = (Guid)item.GetType().GetProperty(nameof(EventWrapper<EventBase>.EventId))?.GetValue(item)!;
                var eventType = (EventBase)item.GetType().GetProperty(nameof(EventWrapper<EventBase>.Event))?.GetValue(item)!;
                var parameter = eventHandlerMethod.GetParameters()[0];

                _correlationId = (Guid?)item.GetType().GetProperty(nameof(EventWrapper<EventBase>.CorrelationId))
                    ?.GetValue(item);

                if (parameter.ParameterType == eventType.GetType())
                {
                    await HandleMethodInvocationAsync(eventHandlerMethod, parameter, eventType, eventId);
                }

                if (parameter.ParameterType == typeof(EventWrapperBase))
                {
                    try
                    {
                        var invokeParameter =
                            new EventWrapper<EventBase>(eventType, eventId, this.GetGrainId())
                            {
                                CorrelationId = _correlationId
                            };
                        var result = eventHandlerMethod.Invoke(this, [invokeParameter]);
                        await (Task)result!;
                    }
                    catch (Exception ex)
                    {
                        // TODO: Make this better.
                        Logger.LogError(ex, "Error invoking method {MethodName} with event type {EventType}",
                            eventHandlerMethod.Name, eventType.GetType().Name);
                    }
                }
            });

            _observers.Add(observer);
        }

        return Task.CompletedTask;
    }

    private Task UpdateInitializeDtoType()
    {
        var initializeMethod = GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SingleOrDefault(IsInitializeMethod);
        if (initializeMethod == null)
        {
            return Task.CompletedTask;
        }

        var parameterType = initializeMethod.GetParameters()[0].ParameterType;
        RaiseEvent(new SetInitializeDtoTypeGEvent
        {
            InitializeDtoType = parameterType
        });
        ConfirmEvents();

        return Task.CompletedTask;
    }

    private IEnumerable<MethodInfo> GetEventHandlerMethods()
    {
        return GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(IsEventHandlerMethod);
    }

    private bool IsEventHandlerMethod(MethodInfo methodInfo)
    {
        return methodInfo.GetParameters().Length == 1 && (
            // Either the method has the EventHandlerAttribute
            // Or is named HandleEventAsync
            //     and the parameter is not EventWrapperBase 
            //     and the parameter is inherited from EventBase
            ((methodInfo.GetCustomAttribute<EventHandlerAttribute>() != null ||
              methodInfo.Name == AevatarGAgentConstants.EventHandlerDefaultMethodName) &&
             methodInfo.GetParameters()[0].ParameterType != typeof(EventWrapperBase) &&
             typeof(EventBase).IsAssignableFrom(methodInfo.GetParameters()[0].ParameterType))
            // Or the method has the AllEventHandlerAttribute and the parameter is EventWrapperBase
            || (methodInfo.GetCustomAttribute<AllEventHandlerAttribute>() != null &&
                methodInfo.GetParameters()[0].ParameterType == typeof(EventWrapperBase))
            // Or the method is for GAgent initialization
            || (methodInfo.Name == AevatarGAgentConstants.InitializeDefaultMethodName &&
                typeof(EventBase).IsAssignableFrom(methodInfo.GetParameters()[0].ParameterType)));
    }

    private bool IsInitializeMethod(MethodInfo methodInfo)
    {
        return methodInfo.GetParameters().Length == 1 &&
               methodInfo.Name == AevatarGAgentConstants.InitializeDefaultMethodName &&
               typeof(EventBase).IsAssignableFrom(methodInfo.GetParameters()[0].ParameterType);
    }

    private async Task HandleMethodInvocationAsync(MethodInfo method, ParameterInfo parameter, EventBase eventType,
        Guid eventId)
    {
        if (IsEventWithResponse(parameter))
        {
            await HandleEventWithResponseAsync(method, eventType, eventId);
        }
        else if (method.ReturnType == typeof(Task))
        {
            try
            {
                var result = method.Invoke(this, [eventType]);
                await (Task)result!;
            }
            catch (Exception ex)
            {
                // TODO: Make this better.
                Logger.LogError(ex, "Error invoking method {MethodName} with event type {EventType}", method.Name,
                    eventType.GetType().Name);
            }
        }
    }

    private bool IsEventWithResponse(ParameterInfo parameter)
    {
        return parameter.ParameterType.BaseType is { IsGenericType: true } &&
               parameter.ParameterType.BaseType.GetGenericTypeDefinition() == typeof(EventWithResponseBase<>);
    }

    private async Task HandleEventWithResponseAsync(MethodInfo method, EventBase eventType, Guid eventId)
    {
        if (method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = method.ReturnType.GetGenericArguments()[0];
            if (typeof(EventBase).IsAssignableFrom(resultType))
            {
                try
                {
                    var eventResult = await (dynamic)method.Invoke(this, [eventType])!;
                    eventResult.CorrelationId = _correlationId;
                    var eventWrapper =
                        new EventWrapper<EventBase>(eventResult, eventId, this.GetGrainId());
                    await PublishAsync(eventWrapper);
                }
                catch (Exception ex)
                {
                    // TODO: Make this better.
                    Logger.LogError(ex, "Error invoking method {MethodName} with event type {EventType}", method.Name,
                        eventType.GetType().Name);
                }
            }
            else
            {
                var errorMessage =
                    $"The event handler of {eventType.GetType()}'s return type needs to be inherited from EventBase.";
                Logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }
        else
        {
            var errorMessage =
                $"The event handler of {eventType.GetType()} needs to have a return value.";
            Logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
    }
}