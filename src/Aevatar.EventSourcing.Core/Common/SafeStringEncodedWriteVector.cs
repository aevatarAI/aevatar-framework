using Orleans.EventSourcing.Common;

namespace Aevatar.EventSourcing.Core.Common;

public static class SafeStringEncodedWriteVector
{
    /// <summary>
    /// Gets one of the bits in writeVector safely
    /// </summary>
    /// <param name="writeVector">The write vector which we want get the bit from</param>
    /// <param name="replica">The replica for which we want to look up the bit</param>
    /// <returns></returns>
    public static bool GetBit(string writeVector, string replica)
    {
        if (string.IsNullOrEmpty(writeVector) || string.IsNullOrEmpty(replica))
            return false;
            
        var pos = writeVector.IndexOf(replica);
        return pos > 0 && writeVector[pos - 1] == ',';
    }

    /// <summary>
    /// Toggle one of the bits in writeVector and return the new value safely
    /// </summary>
    /// <param name="writeVector">The write vector in which we want to flip the bit</param>
    /// <param name="replica">The replica for which we want to flip the bit</param>
    /// <returns>the state of the bit after flipping it</returns>
    public static bool FlipBit(ref string writeVector, string replica)
    {
        writeVector ??= string.Empty;
        
        if (string.IsNullOrEmpty(replica))
            return false;
            
        var pos = writeVector.IndexOf(replica);
        if (pos > 0 && writeVector[pos - 1] == ',')
        {
            // Bit is set, remove it
            var pos2 = writeVector.IndexOf(',', pos + 1);
            if (pos2 == -1)
                pos2 = writeVector.Length;
            writeVector = writeVector.Remove(pos - 1, pos2 - pos + 1);
            return false;
        }
        else
        {
            // Bit is not set, add it
            writeVector = string.Format(",{0}{1}", replica, writeVector);
            return true;
        }
    }
}