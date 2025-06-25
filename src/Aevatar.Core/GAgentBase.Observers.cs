using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Exceptions;
using Aevatar.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core;

public abstract partial class GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
{
    private readonly ConcurrentDictionary<Type, MethodInfo[]> _handlerCache = new();

    protected virtual Task UpdateObserverListAsync(Type type)
    {
        var handlerMethods = GetCachedHandlerMethods(type);

        var observers = ArrayPool<EventWrapperBaseAsyncObserver>.Shared.Rent(handlerMethods.Length);
        try
        {
            var count = 0;
            foreach (var method in handlerMethods)
            {
                try
                {
                    var (parameterType, isResponseHandler) = method.AnalysisMethodMetadata();
                    var observer = CreateMethodObserver(method, parameterType, isResponseHandler);
                    observers[count++] = observer;
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Logger.LogCritical(ex, "Metadata analysis failed for method {Method}", method.Name);
                    throw new InvalidOperationException($"Type load error in {method.Name}", ex);
                }
            }

            try
            {
                _observers.AddRange(observers.Take(count));
            }
            catch (ArgumentNullException ex)
            {
                Logger.LogWarning(ex, "Attempted to add null observer for {Type}", type.Name);
            }
        }
        finally
        {
            ArrayPool<EventWrapperBaseAsyncObserver>.Shared.Return(observers);
        }

        return Task.CompletedTask;
    }

    private MethodInfo[] GetCachedHandlerMethods(Type type)
    {
        try
        {
            return _handlerCache.GetOrAdd(type, t =>
            {
                try
                {
                    var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    return methods.AsParallel()
                        .WithDegreeOfParallelism(Math.Max(1, Environment.ProcessorCount - 1))
                        .Where(IsEventHandlerMethod)
                        .OrderBy(m => m.GetCustomAttribute<EventHandlerAttribute>()?.Priority ?? 0)
                        .ToArray();
                }
                catch (ArgumentNullException ex)
                {
                    Logger.LogError(ex, "Type metadata resolution failed for {Type}", t.Name);
                    return Array.Empty<MethodInfo>();
                }
            });
        }
        catch (Exception ex) when (ex is ArgumentNullException or OverflowException)
        {
            Logger.LogCritical(ex, "Handler cache corruption detected");
            _handlerCache.Clear();
            throw;
        }
    }

    private EventWrapperBaseAsyncObserver CreateMethodObserver(
        MethodInfo method,
        Type parameterType,
        bool isResponseHandler)
    {
        return EventWrapperBaseAsyncObserver.Create(async item =>
        {
            using (Logger.BeginScope(new Dictionary<string, object>
                   {
                       ["GrainId"] = this.GetGrainId(),
                       ["EventWrapperBase"] = item
                   }))
            {
                try
                {
                    var eventId = (Guid)item.GetType().GetProperty(nameof(EventWrapper<TEvent>.EventId))
                        ?.GetValue(item)!;
                    var eventType = (TEvent)item.GetType().GetProperty(nameof(EventWrapper<TEvent>.Event))
                        ?.GetValue(item)!;
                    var grainId = (GrainId)item.GetType().GetProperty(nameof(EventWrapper<TEvent>.GrainId))
                        ?.GetValue(item)!;
                    var publishedTimestamp = (DateTime)item.GetType().GetProperty(nameof(EventWrapper<TEvent>.PublishedTimestampUtc))
                        ?.GetValue(item)!;

                    var eventWrapper = new EventWrapper<TEvent>(eventType, eventId, grainId);
                    eventWrapper.PublishedTimestampUtc = publishedTimestamp;

                    Logger.LogInformation("Handling event {EventWrapper} in method {MethodName}", eventWrapper,
                        method.Name);

                    if (ShouldSkipEvent(eventWrapper, method))
                        return;

                    _correlationId = eventWrapper.Event.CorrelationId;

                    try
                    {
                        await HandleEventWrapper(
                            method,
                            parameterType,
                            eventWrapper,
                            isResponseHandler
                        );
                    }
                    catch (Exception ex) when (ex is EventHandlingException)
                    {
                        Logger.LogError(ex,
                            "Event handling failed | Method:{Method} | EventId:{EventId}",
                            method.Name,
                            eventWrapper.EventId);

                        await PublishAsync(new EventHandlerExceptionEvent
                        {
                            GrainId = this.GetGrainId(),
                            HandleEventType = parameterType,
                            ExceptionMessage = ex.ToString()
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex,
                            "Framework error occured | Method:{Method} | EventId:{EventId}",
                            method.Name,
                            eventWrapper.EventId);

                        await PublishAsync(new GAgentBaseExceptionEvent
                        {
                            GrainId = this.GetGrainId(),
                            ExceptionMessage = ex.ToString()
                        });
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.LogCritical(ex, "Unhandled event processing error");
                    throw;
                }
            }
        }, ServiceProvider, method.Name, parameterType.Name);
    }

    private bool ShouldSkipEvent(EventWrapper<TEvent> eventWrapper, MethodInfo method)
    {
        return eventWrapper.GrainId == this.GetGrainId() && !method.IsSelfHandlingAllowed();
    }

    private async Task HandleEventWrapper(
        MethodInfo method,
        Type parameterType,
        EventWrapper<TEvent> eventWrapper,
        bool isResponseHandler)
    {
        switch (eventWrapper.Event)
        {
            case { } ev when parameterType.BaseType == typeof(EventBase):
                await HandleEvent(method, ev);
                break;
            
            case not null when parameterType == typeof(EventWrapperBase):
                await HandleEventWrapperBase(method, eventWrapper);
                break;

            case { } ev when isResponseHandler:
                await HandleEventWithResponse(method, ev, eventWrapper.EventId);
                break;

            default:
                Logger.LogWarning("Unmatched event type {Type} for method {Method}",
                    eventWrapper.Event!.GetType().Name,
                    method.Name);
                break;
        }
    }

    private async Task HandleEvent(MethodInfo method, TEvent ev)
    {
        try
        {
            await (Task)method.Invoke(this, [ev])!;
        }
        catch (ArgumentException ex)
        {
            Logger.LogError(ex, "Parameter mismatch in {Method}", method.Name);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while invoking {Method}", method.Name);
            throw new EventHandlingException(ex.InnerException?.ToString() ?? ex.ToString(), ex.InnerException ?? ex);
        }
    }

    private async Task HandleEventWrapperBase(MethodInfo method, EventWrapper<TEvent> wrapperBase)
    {
        try
        {
            await (Task)method.Invoke(this, [wrapperBase])!;
        }
        catch (InvalidCastException ex)
        {
            Logger.LogError(ex, "Invalid return type from {Method}", method.Name);
            throw new InvalidOperationException("Handler returned non-task result", ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while invoking {Method}", method.Name);
            throw new EventHandlingException(ex.InnerException?.ToString() ?? ex.ToString(), ex.InnerException ?? ex);
        }
    }

    private async Task HandleEventWithResponse(
        MethodInfo method,
        EventBase ev,
        Guid eventId)
    {
        try
        {
            dynamic result = method.Invoke(this, [ev])!;
            if (result is not Task<EventBase> && !typeof(EventBase).IsAssignableFrom(result.GetType().GetGenericArguments()[0]))
            {
                throw new InvalidOperationException("Response handler must return Task<EventBase or its derived type>");
            }

            var eventResult = await result;
            await PublishResponse(eventResult, eventId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while invoking {Method}", method.Name);
            throw new EventHandlingException(ex.InnerException?.ToString() ?? ex.ToString(), ex.InnerException ?? ex);
        }
    }

    private async Task PublishResponse(EventBase result, Guid eventId)
    {
        result.CorrelationId = _correlationId;
        result.PublisherGrainId = this.GetGrainId();
        var responseWrapper = new EventWrapper<TEvent>(
            (TEvent)result,
            eventId,
            this.GetGrainId());
        responseWrapper.PublishedTimestampUtc = DateTime.UtcNow;

        await PublishAsync(responseWrapper);
    }

    protected virtual IEnumerable<MethodInfo> GetEventHandlerMethods(Type type) =>
        _handlerCache.GetOrAdd(type, t =>
            t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(IsEventHandlerMethod)
                .ToArray());

    protected virtual bool IsEventHandlerMethod(MethodInfo method)
    {
        var param = method.GetParameters();
        if (param.Length != 1) return false;

        var paramType = param[0].ParameterType;
        return paramType switch
        {
            _ when paramType.IsAssignableTo(typeof(TEvent)) =>
                method.HasAttribute<EventHandlerAttribute>() || IsDefaultHandler(method),

            _ when paramType.IsAssignableTo(typeof(EventWrapperBase)) =>
                method.HasAttribute<AllEventHandlerAttribute>(),

            _ when paramType.IsAssignableTo(typeof(ConfigurationBase)) =>
                method.Name == nameof(PerformConfigAsync),

            _ => false
        };

        bool IsDefaultHandler(MethodInfo m) =>
            m.Name == AevatarGAgentConstants.EventHandlerDefaultMethodName &&
            !paramType.IsAbstract;
    }
}