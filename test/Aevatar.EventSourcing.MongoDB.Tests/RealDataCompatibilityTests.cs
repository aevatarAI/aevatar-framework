using System;
using System.Collections.Generic;
using System.Text.Json;
using Aevatar.EventSourcing.MongoDB.Serializers;
using MongoDB.Bson;
using Xunit;

namespace Aevatar.EventSourcing.MongoDB.Tests;

public class RealDataCompatibilityTests
{
    [Fact]
    public void OrleansCompatibleGrainSerializer_CanDeserializeRealMemoryFormat()
    {
        // Arrange - 真实的 Memory 格式数据（简化版）
        var realMemoryData = """
        {
            "__id": "1",
            "__type": "Orleans.EventSourcing.LogStorage.LogStateWithMetaData`1[[Aevatar.Core.Abstractions.StateLogEventBase`1[[Aevatar.Application.Grains.Agents.ChatManager.Chat.GodChatEventLog, Aevatar.Application.Grains]], Aevatar.Core.Abstractions]], Orleans.EventSourcing",
            "Log": {
                "__type": "System.Collections.Generic.List`1[[Aevatar.Core.Abstractions.StateLogEventBase`1[[Aevatar.Application.Grains.Agents.ChatManager.Chat.GodChatEventLog, Aevatar.Application.Grains]], Aevatar.Core.Abstractions]], System.Private.CoreLib",
                "__values": [
                    {
                        "__id": "2",
                        "__type": "Aevatar.GAgents.AIGAgent.Agent.AIGAgentBase+SetPromptTemplateStateLogEvent",
                        "PromptTemplate": "Test prompt template",
                        "Id": "00000000-0000-0000-0000-000000000000",
                        "Ctime": "0001-01-01T00:00:00"
                    }
                ]
            },
            "GlobalVersion": 5,
            "WriteVector": ",godgptSiloCluster"
        }
        """;

        var bsonString = new BsonString(realMemoryData);
        var serializer = new OrleansCompatibleGrainSerializer();

        // Act - 尝试提取事件列表
        var result = serializer.Deserialize<List<object>>(bsonString);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result); // 应该有一个事件
    }

    [Fact]
    public void OrleansCompatibleGrainSerializer_CanHandleSimpleJsonFormat()
    {
        // Arrange - 简单的 JSON 格式（单个事件）
        var simpleJson = """
        {
            "PromptTemplate": "Test prompt",
            "Id": "00000000-0000-0000-0000-000000000000",
            "Ctime": "0001-01-01T00:00:00"
        }
        """;

        var bsonString = new BsonString(simpleJson);
        var serializer = new OrleansCompatibleGrainSerializer();

        // Act
        var result = serializer.Deserialize<object>(bsonString);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CompatibleGrainSerializer_CanDeserializeRealMemoryFormat()
    {
        // Arrange - 使用原始的兼容序列化器测试简单格式
        var simpleJson = """
        {
            "PromptTemplate": "Test prompt template",
            "Id": "00000000-0000-0000-0000-000000000000",
            "Ctime": "0001-01-01T00:00:00"
        }
        """;

        var bsonString = new BsonString(simpleJson);
        var serializer = new CompatibleGrainSerializer();

        // Act
        var result = serializer.Deserialize<object>(bsonString);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void TestComplexTypeDeserialization()
    {
        // 测试具体的事件类型
        var eventJson = """
        {
            "__id": "2",
            "__type": "Aevatar.GAgents.AIGAgent.Agent.AIGAgentBase+SetPromptTemplateStateLogEvent",
            "PromptTemplate": "Test prompt template",
            "Id": "00000000-0000-0000-0000-000000000000",
            "Ctime": "0001-01-01T00:00:00"
        }
        """;

        // 测试 System.Text.Json 是否能处理这种格式
        var result = JsonSerializer.Deserialize<object>(eventJson);
        Assert.NotNull(result);
    }

    [Fact]
    public void TestLogStateWithMetaDataStructure()
    {
        // 测试完整的 LogStateWithMetaData 结构
        var logStateJson = """
        {
            "Log": [
                {
                    "PromptTemplate": "Test",
                    "Id": "00000000-0000-0000-0000-000000000000",
                    "Ctime": "0001-01-01T00:00:00"
                }
            ],
            "GlobalVersion": 5,
            "WriteVector": ",godgptSiloCluster"
        }
        """;

        // 简化版本的结构测试
        var result = JsonSerializer.Deserialize<SimpleLogStateWithMetaData>(logStateJson);
        Assert.NotNull(result);
        Assert.Equal(5, result.GlobalVersion);
        Assert.Equal(",godgptSiloCluster", result.WriteVector);
        Assert.NotNull(result.Log);
        Assert.Single(result.Log);
    }

    // 简化的测试类型
    public class SimpleLogStateWithMetaData
    {
        public object[] Log { get; set; } = Array.Empty<object>();
        public int GlobalVersion { get; set; }
        public string WriteVector { get; set; } = string.Empty;
    }
}