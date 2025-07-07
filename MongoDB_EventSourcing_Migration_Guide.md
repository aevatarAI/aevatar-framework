# MongoDB EventSourcing 迁移指南

## 概述

本指南帮助您从Orleans原生EventSourcing迁移到MongoDB EventSourcing，同时保持向后兼容性。

## 支持的迁移路径

### 1. Orleans LogStateWithMetaData → MongoDB
- **源格式**: Orleans原生EventSourcing (LogStateWithMetaData)
- **目标格式**: MongoDB分离存储 (每个事件单独文档)
- **兼容性**: 自动检测和透明迁移

### 2. Memory Storage → MongoDB
- **源格式**: 内存存储的JSON格式
- **目标格式**: MongoDB BSON格式
- **兼容性**: 自动格式转换

## 快速开始

### 1. 安装依赖

```xml
<PackageReference Include="Aevatar.EventSourcing.MongoDB" Version="1.5.1-event.4" />
```

### 2. 配置服务

#### 标准MongoDB配置
```csharp
services.AddMongoDbBasedLogConsistencyProviderAsDefault(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.Database = "EventSourcing";
});
```

#### Orleans兼容配置
```csharp
services.AddOrleansCompatibleMongoDbBasedLogConsistencyProviderAsDefault(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.Database = "EventSourcing";
});
```

### 3. 配置Silo

#### 使用ServiceCollection配置
```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering()
               .ConfigureServices(services =>
               {
                   services.AddOrleansCompatibleMongoDbBasedLogConsistencyProviderAsDefault(options =>
                   {
                       options.ConnectionString = "mongodb://localhost:27017";
                       options.Database = "EventSourcing";
                   });
               });
});
```

#### 使用SiloBuilder扩展方法
```csharp
siloBuilder.AddOrleansCompatibleMongoDbBasedLogConsistencyProviderAsDefault(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.Database = "EventSourcing";
});
```

## 迁移策略

### 自动迁移 (推荐)

系统会自动检测Orleans格式的数据并在后台透明迁移：

```csharp
// 无需额外代码，系统自动处理
var events = await grain.ReadEventsAsync(0, 100);
```

**优点**:
- 零停机迁移
- 透明处理
- 自动格式检测

**注意事项**:
- 迁移过程中会有轻微的性能开销
- 建议在低峰期进行大量数据迁移

### 手动迁移

如果需要更精细的控制，可以实现自定义迁移逻辑：

```csharp
public class CustomMigrationService
{
    private readonly IMongoCollection<BsonDocument> _collection;
    
    public async Task MigrateGrainAsync(GrainId grainId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("GrainId", grainId.ToString());
        var orleansDocument = await _collection.Find(filter).FirstOrDefaultAsync();
        
        if (orleansDocument != null && IsOrleansFormat(orleansDocument))
        {
            // 提取事件
            var events = ExtractEvents(orleansDocument);
            
            // 创建新格式文档
            var newDocuments = CreateMongoDbFormatDocuments(grainId, events);
            
            // 使用事务替换
            using var session = await _mongoClient.StartSessionAsync();
            await session.WithTransactionAsync(async (session, ct) =>
            {
                await _collection.DeleteOneAsync(session, filter, cancellationToken: ct);
                await _collection.InsertManyAsync(session, newDocuments, cancellationToken: ct);
                return true;
            });
        }
    }
}
```

## 配置选项

### MongoDbStorageOptions

```csharp
public class MongoDbStorageOptions
{
    /// <summary>
    /// MongoDB连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    
    /// <summary>
    /// 数据库名称
    /// </summary>
    public string Database { get; set; } = "OrleansEventSourcing";
    
    /// <summary>
    /// 集合名称模板
    /// </summary>
    public string CollectionName { get; set; } = "EventSourcing";
    
    /// <summary>
    /// 序列化器配置
    /// </summary>
    public IGrainStateSerializer GrainStateSerializer { get; set; }
    
    /// <summary>
    /// MongoDB客户端设置
    /// </summary>
    public MongoClientSettings ClientSettings { get; set; }
}
```

### 建议的生产环境配置

```csharp
services.AddOrleansCompatibleMongoDbBasedLogConsistencyProviderAsDefault(options =>
{
    options.ConnectionString = "mongodb://mongo1:27017,mongo2:27017,mongo3:27017";
    options.Database = "EventSourcing";
    options.ClientSettings = new MongoClientSettings
    {
        ReplicaSetName = "rs0",
        ReadPreference = ReadPreference.SecondaryPreferred,
        WriteConcern = WriteConcern.WMajority,
        ReadConcern = ReadConcern.Majority
    };
});
```

## 迁移验证

### 1. 数据完整性验证

```csharp
public async Task ValidateMigrationAsync(GrainId grainId)
{
    // 比较迁移前后的事件数量
    var originalEventCount = await GetOriginalEventCountAsync(grainId);
    var migratedEventCount = await GetMigratedEventCountAsync(grainId);
    
    if (originalEventCount != migratedEventCount)
    {
        throw new InvalidOperationException($"Event count mismatch for {grainId}");
    }
    
    // 验证事件内容一致性
    var originalEvents = await GetOriginalEventsAsync(grainId);
    var migratedEvents = await GetMigratedEventsAsync(grainId);
    
    for (int i = 0; i < originalEvents.Count; i++)
    {
        if (!EventsAreEqual(originalEvents[i], migratedEvents[i]))
        {
            throw new InvalidOperationException($"Event content mismatch at index {i}");
        }
    }
}
```

### 2. 性能基准测试

```csharp
public async Task BenchmarkMigrationAsync()
{
    var stopwatch = Stopwatch.StartNew();
    
    // 执行迁移
    await MigrateAllGrainsAsync();
    
    stopwatch.Stop();
    
    Console.WriteLine($"Migration completed in {stopwatch.Elapsed}");
    Console.WriteLine($"Average time per grain: {stopwatch.Elapsed.TotalMilliseconds / grainCount}ms");
}
```

## 故障排除

### 常见问题

#### 1. 迁移失败
**现象**: 日志中出现迁移失败的警告
**原因**: 并发访问或网络问题
**解决**: 检查MongoDB连接和权限设置

#### 2. 数据不一致
**现象**: 读取的事件数量不正确
**原因**: 迁移过程中并发写入
**解决**: 暂停写入操作，重新运行迁移

#### 3. 性能问题
**现象**: 读取速度明显变慢
**原因**: 缺乏适当的索引
**解决**: 创建必要的MongoDB索引

### 诊断命令

```csharp
// 检查迁移状态
public async Task CheckMigrationStatusAsync()
{
    var filter = Builders<BsonDocument>.Filter.Regex("Data", new BsonRegularExpression("LogStateWithMetaData"));
    var orleansFormatCount = await _collection.CountDocumentsAsync(filter);
    
    Console.WriteLine($"Remaining Orleans format documents: {orleansFormatCount}");
}

// 检查索引
public async Task CheckIndexesAsync()
{
    var indexes = await _collection.Indexes.ListAsync();
    await indexes.ForEachAsync(index =>
    {
        Console.WriteLine($"Index: {index}");
    });
}
```

## 最佳实践

### 1. 迁移前准备
- 备份现有数据
- 在测试环境中验证迁移过程
- 准备回滚计划

### 2. 迁移过程
- 在低峰期进行迁移
- 监控系统性能
- 验证数据完整性

### 3. 迁移后优化
- 创建适当的索引
- 调整连接池设置
- 监控系统性能

### 4. 监控指标
- 迁移进度
- 错误率
- 性能指标
- 数据一致性

## 索引建议

```javascript
// 创建必要的索引
db.EventSourcing.createIndex({ "GrainId": 1, "Version": 1 });
db.EventSourcing.createIndex({ "GrainId": 1 });
db.EventSourcing.createIndex({ "Version": 1 });
```

## 支持

如果遇到问题，请：
1. 查看日志文件
2. 检查MongoDB连接
3. 验证配置设置
4. 提交Issue到GitHub仓库 