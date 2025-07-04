using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Aevatar.EventSourcing.MongoDB.Serializers;
using MongoDB.Bson;
using Xunit;

namespace Aevatar.EventSourcing.MongoDB.Tests;

public static class DateTimeExtensions
{
    public static DateTime TruncateToMilliseconds(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 
            dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, dateTime.Kind);
    }
}

public class CompatibilityTests
{
    public class TestEvent
    {
        public string Message { get; set; } = string.Empty;
        public int Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    [Fact]
    public void CompatibleGrainSerializer_CanDeserializeJsonString()
    {
        // Arrange - Memory format (JSON string)
        var testEvent = new TestEvent
        {
            Message = "Test Message",
            Value = 42,
            Timestamp = DateTime.UtcNow
        };
        
        var jsonString = JsonSerializer.Serialize(testEvent);
        var bsonString = new BsonString(jsonString);
        
        var serializer = new CompatibleGrainSerializer();
        
        // Act
        var result = serializer.Deserialize<TestEvent>(bsonString);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(testEvent.Message, result.Message);
        Assert.Equal(testEvent.Value, result.Value);
        Assert.Equal(testEvent.Timestamp, result.Timestamp);
    }

    [Fact]
    public void CompatibleGrainSerializer_CanDeserializeBsonDocument()
    {
        // Arrange - MongoDB format (BSON document)
        var testEvent = new TestEvent
        {
            Message = "Test Message",
            Value = 42,
            Timestamp = DateTime.UtcNow.TruncateToMilliseconds() // BSON has millisecond precision
        };
        
        var bsonDocument = testEvent.ToBsonDocument();
        
        var serializer = new CompatibleGrainSerializer();
        
        // Act
        var result = serializer.Deserialize<TestEvent>(bsonDocument);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(testEvent.Message, result.Message);
        Assert.Equal(testEvent.Value, result.Value);
        Assert.Equal(testEvent.Timestamp, result.Timestamp);
    }

    [Fact]
    public void CompatibleGrainSerializer_SerializesToBsonDocument()
    {
        // Arrange
        var testEvent = new TestEvent
        {
            Message = "Test Message",
            Value = 42,
            Timestamp = DateTime.UtcNow.TruncateToMilliseconds() // BSON has millisecond precision
        };
        
        var serializer = new CompatibleGrainSerializer();
        
        // Act
        var result = serializer.Serialize(testEvent);
        
        // Assert
        Assert.True(result.IsBsonDocument);
        
        // Verify we can deserialize it back
        var deserialized = serializer.Deserialize<TestEvent>(result);
        Assert.Equal(testEvent.Message, deserialized.Message);
        Assert.Equal(testEvent.Value, deserialized.Value);
        Assert.Equal(testEvent.Timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void CompatibleGrainSerializer_HandlesNullValues()
    {
        var serializer = new CompatibleGrainSerializer();
        
        // Test null serialization
        var nullResult = serializer.Serialize<TestEvent>(null!);
        Assert.True(nullResult.IsBsonNull);
        
        // Test null deserialization
        var nullDeserialized = serializer.Deserialize<TestEvent>(BsonNull.Value);
        Assert.Null(nullDeserialized);
    }

    [Fact]
    public void CompatibleGrainSerializer_ThrowsOnInvalidFormat()
    {
        var serializer = new CompatibleGrainSerializer();
        
        // Test with invalid BSON value (number)
        var invalidValue = new BsonInt32(42);
        
        Assert.Throws<InvalidOperationException>(() => 
            serializer.Deserialize<TestEvent>(invalidValue));
    }

    [Theory]
    [InlineData("Memory")]
    [InlineData("MongoDB")]
    public void CompatibleGrainSerializer_RoundTripConsistency(string format)
    {
        // Arrange
        var baseTime = DateTime.UtcNow.TruncateToMilliseconds();
        var testEvents = new List<TestEvent>
        {
            new() { Message = "Event 1", Value = 1, Timestamp = baseTime },
            new() { Message = "Event 2", Value = 2, Timestamp = baseTime.AddMinutes(1) },
            new() { Message = "Event 3", Value = 3, Timestamp = baseTime.AddMinutes(2) }
        };
        
        var serializer = new CompatibleGrainSerializer();
        
        foreach (var testEvent in testEvents)
        {
            BsonValue serializedValue;
            
            if (format == "Memory")
            {
                // Simulate Memory format - serialize to JSON string then to BSON string
                var jsonString = JsonSerializer.Serialize(testEvent);
                serializedValue = new BsonString(jsonString);
            }
            else
            {
                // MongoDB format - serialize to BSON document
                serializedValue = serializer.Serialize(testEvent);
            }
            
            // Act - Deserialize
            var result = serializer.Deserialize<TestEvent>(serializedValue);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(testEvent.Message, result.Message);
            Assert.Equal(testEvent.Value, result.Value);
            Assert.Equal(testEvent.Timestamp, result.Timestamp);
        }
    }
}