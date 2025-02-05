using System.Reflection;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
{
    protected virtual Task UpdateObserverListAsync(Type type)
    {
        var eventHandlerMethods = GetEventHandlerMethods(type);

        foreach (var eventHandlerMethod in eventHandlerMethods)
        {
            var parameter = eventHandlerMethod.GetParameters()[0];
            var parameterType = parameter.ParameterType;
            var parameterTypeName = parameterType.Name;
            var observer = new EventWrapperBaseAsyncObserver(async item =>
            {
                var grainId = (GrainId)item.GetType().GetProperty(nameof(EventWrapper<TEvent>.GrainId))?.GetValue(item)!;
                if (grainId == this.GetGrainId() && eventHandlerMethod.Name != nameof(ForwardEventAsync) &&
                    eventHandlerMethod.Name != nameof(PerformConfigAsync))
                {
                    // Skip the event if it is sent by itself.
                    return;
                }

                try
                {
                    var eventId = (Guid)item.GetType().GetProperty(nameof(EventWrapper<TEvent>.EventId))
                        ?.GetValue(item)!;
                    var eventType = (TEvent)item.GetType().GetProperty(nameof(EventWrapper<TEvent>.Event))
                        ?.GetValue(item)!;

                    if (parameterType == eventType.GetType())
                    {
                        await HandleMethodInvocationAsync(eventHandlerMethod, parameter, eventType, eventId);
                    }

                    if (parameterType == typeof(EventWrapperBase))
                    {
                        try
                        {
                            var invokeParameter =
                                new EventWrapper<TEvent>(eventType, eventId, this.GetGrainId());
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
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error invoking method {MethodName} with event type {EventType}",
                        eventHandlerMethod.Name, parameterTypeName);
                }
            })
            {
                MethodName = eventHandlerMethod.Name,
                ParameterTypeName = parameterTypeName
            };

            _observers.Add(observer);
        }

        return Task.CompletedTask;
    }

    protected virtual IEnumerable<MethodInfo> GetEventHandlerMethods(Type type)
    {
        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(IsEventHandlerMethod);
    }

    protected virtual bool IsEventHandlerMethod(MethodInfo methodInfo)
    {
        return methodInfo.GetParameters().Length == 1 && (
            // Either the method has the EventHandlerAttribute
            // Or is named HandleEventAsync
            //     and the parameter is not EventWrapperBase 
            //     and the parameter is inherited from EventBase
            ((methodInfo.GetCustomAttribute<EventHandlerAttribute>() != null ||
              methodInfo.Name == AevatarGAgentConstants.EventHandlerDefaultMethodName) &&
             methodInfo.GetParameters()[0].ParameterType != typeof(EventWrapperBase) &&
             typeof(TEvent).IsAssignableFrom(methodInfo.GetParameters()[0].ParameterType))
            // Or the method has the AllEventHandlerAttribute and the parameter is EventWrapperBase
            || (methodInfo.GetCustomAttribute<AllEventHandlerAttribute>() != null &&
                methodInfo.GetParameters()[0].ParameterType == typeof(EventWrapperBase))
            // Or the method is for GAgent initialization
            || (methodInfo.Name == nameof(PerformConfigAsync) &&
                typeof(ConfigurationBase).IsAssignableFrom(methodInfo.GetParameters()[0].ParameterType))
        );
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
                    eventResult.PublisherGrainId = this.GetGrainId();
                    var eventWrapper =
                        new EventWrapper<TEvent>(eventResult, eventId, this.GetGrainId());
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