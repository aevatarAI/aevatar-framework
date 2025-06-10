using System;
using System.Collections.Generic;
using Aevatar.EventSourcing.MongoDB.Serializers;
using MongoDB.Bson;
using Orleans.Storage;
using Shouldly;
using Xunit;

namespace Aevatar.EventSourcing.MongoDB.Tests;

/// <summary>
/// Tests for BsonGrainSerializer to verify correct serialization/deserialization
/// </summary>
public class BsonGrainSerializerTests
{
    private readonly BsonGrainSerializer _serializer;

    public BsonGrainSerializerTests()
    {
        _serializer = new BsonGrainSerializer();
    }

    [Fact]
    public void Serialize_Deserialize_SimpleClass_Works()
    {
        // Arrange
        // Test data: Simple class with basic properties - ID: 42, Name: "Test Name"
        var testObject = new TestClass
        {
            Id = 42,
            Name = "Test Name",
            CreatedAt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        BsonValue serialized = _serializer.Serialize(testObject);
        var deserialized = _serializer.Deserialize<TestClass>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(testObject.Id, deserialized.Id);
        Assert.Equal(testObject.Name, deserialized.Name);
        Assert.Equal(testObject.CreatedAt, deserialized.CreatedAt);
    }

    [Fact]
    public void Serialize_Deserialize_ComplexClass_Works()
    {
        // Arrange
        // Test data: Complex class with nested objects and collections
        var testObject = new ComplexTestClass
        {
            Id = Guid.Parse("7f9c2742-5df1-42fb-96f9-7c6d5e0a2de4"),
            Name = "Complex Test",
            Items = new List<string> { "Item1", "Item2", "Item3" },
            NestedObject = new TestClass
            {
                Id = 99,
                Name = "Nested Object",
                CreatedAt = new DateTime(2023, 2, 15, 10, 30, 0, DateTimeKind.Utc)
            },
            Properties = new Dictionary<string, object>
            {
                { "IntProp", 42 },
                { "StringProp", "Value" },
                { "BoolProp", true }
            }
        };

        // Act
        BsonValue serialized = _serializer.Serialize(testObject);
        var deserialized = _serializer.Deserialize<ComplexTestClass>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(testObject.Id, deserialized.Id);
        Assert.Equal(testObject.Name, deserialized.Name);
        Assert.Equal(testObject.Items.Count, deserialized.Items.Count);
        
        // Check nested object
        Assert.NotNull(deserialized.NestedObject);
        Assert.Equal(testObject.NestedObject.Id, deserialized.NestedObject.Id);
        Assert.Equal(testObject.NestedObject.Name, deserialized.NestedObject.Name);
        
        // Check dictionary
        Assert.NotNull(deserialized.Properties);
        Assert.Equal(testObject.Properties.Count, deserialized.Properties.Count);
        Assert.Equal(42, deserialized.Properties["IntProp"]);
        Assert.Equal("Value", deserialized.Properties["StringProp"]);
        Assert.Equal(true, deserialized.Properties["BoolProp"]);
    }

    [Fact]
    public void Serialize_Deserialize_EmptyObject_Works()
    {
        // Test data: Empty object with default values
        
        // Create an empty object
        var emptyObject = new TestClass();
        
        // Act
        BsonValue serialized = _serializer.Serialize(emptyObject);
        var deserialized = _serializer.Deserialize<TestClass>(serialized);
        
        // Assert - empty object should be properly deserialized
        Assert.NotNull(deserialized);
        Assert.Equal(0, deserialized.Id);
        Assert.Equal(string.Empty, deserialized.Name);
        Assert.Equal(default, deserialized.CreatedAt);
    }

    [Fact]
    public void SerializeDeserialize_SimpleObject_WorksCorrectly()
    {
        // Arrange
        var serializer = new BsonGrainSerializer();
        var testObj = new TestClass
        {
            StringProp = "Test String",
            IntProp = 42,
            BoolProp = true
        };

        // Act
        var serialized = serializer.Serialize(testObj);
        var deserialized = serializer.Deserialize<TestClass>(serialized);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.StringProp.ShouldBe(testObj.StringProp);
        deserialized.IntProp.ShouldBe(testObj.IntProp);
        deserialized.BoolProp.ShouldBe(testObj.BoolProp);
    }

    [Fact]
    public void Deserialize_FullDocument_ThrowsException_WhenPassedDirectly()
    {
        // Arrange
        var serializer = new BsonGrainSerializer();
        
        // Create a document similar to what would be stored in MongoDB
        var document = new BsonDocument
        {
            ["GrainId"] = "TestGrain/123",
            ["Version"] = 1,
            ["data"] = new BsonDocument
            {
                ["StringProp"] = "Test String",
                ["IntProp"] = 42,
                ["BoolProp"] = true
            }
        };

        // Act & Assert
        // This should throw an exception since we're trying to deserialize the full document
        // instead of just the "data" part
        Should.Throw<FormatException>(() => serializer.Deserialize<TestClass>(document));
    }

    [Fact]
    public void Deserialize_DataField_WorksCorrectly()
    {
        // Arrange
        var serializer = new BsonGrainSerializer();
        
        // Create just the data field as would be extracted from document
        var dataField = new BsonDocument
        {
            ["StringProp"] = "Test String",
            ["IntProp"] = 42,
            ["BoolProp"] = true
        };

        // Act
        var deserialized = serializer.Deserialize<TestClass>(dataField);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.StringProp.ShouldBe("Test String");
        deserialized.IntProp.ShouldBe(42);
        deserialized.BoolProp.ShouldBe(true);
    }

    [Fact]
    public void Deserialize_BsonDocumentWithDataField_SimulatesMongoDbStorage()
    {
        // Arrange - This test specifically tests how MongoDbLogConsistentStorage deserializes
        var serializer = new BsonGrainSerializer();
        
        // Create a document with the same structure as what's stored/retrieved in MongoDB
        var document = new BsonDocument
        {
            ["GrainId"] = "TestGrain/123",
            ["Version"] = 1,
            ["data"] = new BsonDocument
            {
                ["StringProp"] = "Test String",
                ["IntProp"] = 42,
                ["BoolProp"] = true
            }
        };

        // Act - Simulate what happens in MongoDbLogConsistentStorage.ReadAsync
        // First we get the document from MongoDB
        // Then we should extract the "data" field
        var dataField = document["data"];
        
        // Then deserialize just that field
        var deserialized = serializer.Deserialize<TestClass>(dataField);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.StringProp.ShouldBe("Test String");
        deserialized.IntProp.ShouldBe(42);
        deserialized.BoolProp.ShouldBe(true);
    }

    [Fact]
    public void MongoDbDocumentStructure_VerifyDataExtractionAndParsing()
    {
        // This test simulates the MongoDB document structure and verifies that
        // extracting and deserializing the 'data' field works as expected
        
        // Arrange - create a document that matches MongoDB structure
        var document = new BsonDocument
        {
            { "GrainId", "test/123" },
            { "Version", 42 },
            { "data", new BsonDocument
                {
                    { "StringProp", "Test String" },
                    { "IntProp", 42 },
                    { "BoolProp", true }
                }
            }
        };
        
        var serializer = new BsonGrainSerializer();
        
        // Act & Assert - Direct deserialization of whole document should fail
        var exception = Assert.Throws<FormatException>(() => 
            serializer.Deserialize<TestClass>(document));
        
        // Now extract just the data field and deserialize - this should work
        var dataField = document["data"];
        var result = serializer.Deserialize<TestClass>(dataField);
        
        // Verify the deserialized object has the correct properties
        Assert.NotNull(result);
        Assert.Equal("Test String", result.StringProp);
        Assert.Equal(42, result.IntProp);
        Assert.Equal(true, result.BoolProp);
    }

    private class TestClass
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string StringProp { get; set; } = string.Empty;
        public int IntProp { get; set; }
        public bool BoolProp { get; set; }
    }

    private class ComplexTestClass
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Items { get; set; } = new();
        public TestClass NestedObject { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
    }
} 