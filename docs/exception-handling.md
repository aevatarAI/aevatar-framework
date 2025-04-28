# GAgent 异常捕获与发布

本文档介绍了 Aevatar.Core 中的异常捕获与发布功能，该功能用于捕获 GAgent EventHandler 中的异常，并将其发布到专用的 Orleans Stream。

## 功能概述

异常捕获与发布功能提供以下能力：

1. 自动捕获 GAgent EventHandler 中的异常
2. 收集异常上下文信息，包括：
   - 异常类型与消息
   - 异常堆栈跟踪
   - 发生时间
   - 来源 Grain ID
   - 处理方法名
   - 触发异常的事件 ID 和类型
   - 额外上下文数据
3. 将异常信息通过专用的 Orleans Stream 发布
4. 使用独立的 Kafka Topic 处理异常，与业务 Topic 隔离

## 组件说明

该功能主要由以下组件组成：

1. **GAgentExceptionEvent**：表示异常事件的数据结构，包含异常的详细信息
2. **异常流**：专用于发布异常的 Orleans Stream，默认命名空间为 `ExceptionStream`
3. **异常捕获机制**：修改后的 `GAgentAsyncObserver` 和 `EventWrapperBaseAsyncObserver` 类，用于捕获事件处理过程中的异常
4. **异常发布方法**：GAgentBase 中的 `InvokeWithExceptionPublishingAsync` 方法，用于包装可能抛出异常的代码

## 使用方法

### 订阅异常流

要接收和处理 GAgent 发布的异常，您需要订阅异常流：

```csharp
// 获取异常流
var streamId = StreamId.Create(AevatarGAgentConstants.DefaultExceptionStreamNamespace, grainId);
var exceptionStream = streamProvider.GetStream<EventWrapperBase>(streamId);

// 订阅异常流
await exceptionStream.SubscribeAsync(new ExceptionStreamObserver());

// 异常流观察者实现
public class ExceptionStreamObserver : IAsyncObserver<EventWrapperBase>
{
    public async Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
    {
        // 提取异常事件
        if (item is EventWrapper<GAgentExceptionEvent> wrapper)
        {
            var exceptionEvent = wrapper.Event;
            
            // 处理异常事件
            Console.WriteLine($"捕获到异常: {exceptionEvent.ExceptionType}");
            Console.WriteLine($"消息: {exceptionEvent.ExceptionMessage}");
            Console.WriteLine($"时间: {exceptionEvent.Timestamp}");
            Console.WriteLine($"来源: {exceptionEvent.SourceGrainId}, 方法: {exceptionEvent.SourceMethod}");
            
            // 记录或处理异常...
        }
    }
    
    public Task OnCompletedAsync() => Task.CompletedTask;
    public Task OnErrorAsync(Exception ex) => Task.CompletedTask;
}
```

### 在自定义代码中使用异常发布功能

您可以在自己的 GAgent 代码中使用 `InvokeWithExceptionPublishingAsync` 方法来包装可能抛出异常的代码：

```csharp
public async Task DoSomethingRiskyAsync()
{
    await InvokeWithExceptionPublishingAsync(async () => 
    {
        // 您的代码...
        await RiskyOperationAsync();
        // 更多代码...
    }, nameof(DoSomethingRiskyAsync));
}
```

这样，如果 `RiskyOperationAsync` 抛出异常，它将被捕获并发布到异常流。

### 自定义异常上下文数据

您可以在异常中添加自定义上下文数据，这些数据会被包含在发布的异常事件中：

```csharp
public async Task ProcessWithContextAsync(string userId, string operationId)
{
    try
    {
        // 您的代码...
    }
    catch (Exception ex)
    {
        // 添加自定义上下文数据
        ex.Data["UserId"] = userId;
        ex.Data["OperationId"] = operationId;
        
        // 重新抛出异常，它将被 InvokeWithExceptionPublishingAsync 捕获
        throw;
    }
}
```

## 配置

默认情况下，异常流使用 `ExceptionStream` 命名空间，并且使用 grain ID 作为流 ID。Kafka 主题前缀默认为 `exceptions-`。

这些默认值定义在 `AevatarGAgentConstants` 类中：

```csharp
public static class AevatarGAgentConstants
{
    // ...
    public const string DefaultExceptionStreamNamespace = "ExceptionStream";
    public const string DefaultExceptionKafkaTopicPrefix = "exceptions-";
    // ...
}
```

## 最佳实践

1. **合理处理异常**：虽然异常会被捕获并发布，但您仍应在适当的地方处理异常，以确保应用程序的稳定性
2. **添加有用的上下文**：使用 Exception.Data 添加有助于诊断问题的上下文信息
3. **实现异常监控**：创建专门的服务来监控异常流，并发送警报或执行自动恢复操作
4. **定期检查异常流**：定期检查异常流，以识别潜在的系统问题
5. **不要滥用异常流**：异常流应用于处理真正的异常情况，而不是正常的业务流程

## 示例

### 完整的异常处理服务示例

```csharp
public class ExceptionMonitoringService : BackgroundService
{
    private readonly IStreamProvider _streamProvider;
    private readonly ILogger<ExceptionMonitoringService> _logger;
    
    public ExceptionMonitoringService(
        IStreamProvider streamProvider,
        ILogger<ExceptionMonitoringService> logger)
    {
        _streamProvider = streamProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 创建一个全局异常观察者，用于监控所有GAgent的异常
        var globalStreamId = StreamId.Create(
            AevatarGAgentConstants.DefaultExceptionStreamNamespace,
            "global"
        );
        
        var exceptionStream = _streamProvider.GetStream<EventWrapperBase>(globalStreamId);
        
        // 订阅异常流
        await exceptionStream.SubscribeAsync(
            new GlobalExceptionObserver(_logger),
            null,
            stoppingToken
        );
        
        // 保持服务运行
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    
    private class GlobalExceptionObserver : IAsyncObserver<EventWrapperBase>
    {
        private readonly ILogger _logger;
        
        public GlobalExceptionObserver(ILogger logger)
        {
            _logger = logger;
        }
        
        public Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
        {
            if (EventWrapperHelper.GetProperty<GAgentExceptionEvent>(item, "Event") is GAgentExceptionEvent exEvent)
            {
                _logger.LogError(
                    "GAgent异常: {ExceptionType} 在 {SourceGrainId}.{SourceMethod} - {Message}",
                    exEvent.ExceptionType,
                    exEvent.SourceGrainId,
                    exEvent.SourceMethod,
                    exEvent.ExceptionMessage
                );
                
                // 这里可以添加更多处理，如发送警报、记录到数据库等
            }
            
            return Task.CompletedTask;
        }
        
        public Task OnCompletedAsync() => Task.CompletedTask;
        public Task OnErrorAsync(Exception ex) => Task.CompletedTask;
    }
} 