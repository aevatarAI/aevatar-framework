using Orleans;
using Orleans.EventSourcing.Common;

namespace Aevatar.EventSourcing.Core.Snapshot;

[Serializable]
[GenerateSerializer]
public sealed class ViewStateSnapshotWithMetadata<TLogView>
    where TLogView : class, new()
{
    [Id(0)] public TLogView Snapshot { get; set; } = new();
    [Id(1)] public int SnapshotVersion { get; set; } = 0;
    [Id(2)] public string WriteVector { get; set; } = string.Empty;

    public ViewStateSnapshotWithMetadata()
    {

    }

    public ViewStateSnapshotWithMetadata(TLogView initialState)
    {
        Snapshot = initialState;
    }

    public bool GetBit(string replica)
    {
        return StringEncodedWriteVector.GetBit(WriteVector, replica);
    }

    public bool FlipBit(string replica)
    {
        var str = WriteVector;
        var result = StringEncodedWriteVector.FlipBit(ref str, replica);
        WriteVector = str;
        return result;
    }
}