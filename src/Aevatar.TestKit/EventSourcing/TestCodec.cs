using Orleans.Serialization.Codecs;
using Orleans.Serialization.Buffers;
using System.Buffers;
using Orleans.Serialization.WireProtocol;

public class TestCodec<T> : IFieldCodec<T>
{
    public void WriteField<TBufferWriter>(ref Writer<TBufferWriter> writer, uint fieldIdDelta, Type expectedType, T value) where TBufferWriter : IBufferWriter<byte>
    {
        // Implement serialization logic here
    }

    public T ReadValue<TInput>(ref Reader<TInput> reader, Field field)
    {
        return default;
    }
}