using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;

namespace Aevatar.EventSourcing.MongoDB;

public class TypeBsonSerializer : SerializerBase<Type>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Type value)
    {
        context.Writer.WriteStartDocument();
        context.Writer.WriteName("AssemblyQualifiedName");
        context.Writer.WriteString(value.AssemblyQualifiedName ?? string.Empty);
        context.Writer.WriteEndDocument();
    }

    public override Type Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        context.Reader.ReadStartDocument();
        var assemblyQualifiedName = context.Reader.ReadString();
        context.Reader.ReadEndDocument();
        return Type.GetType(assemblyQualifiedName, throwOnError: false)!;
    }
} 