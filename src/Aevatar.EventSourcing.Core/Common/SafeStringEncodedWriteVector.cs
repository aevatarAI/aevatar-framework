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
        if (pos == -1)
            return false;
            
        // Handle case where replica is at position 0 (WriteVector starts with comma)
        if (pos == 0)
            return writeVector.Length == replica.Length || writeVector[replica.Length] == ',';
            
        // Handle normal case where replica is preceded by comma
        return writeVector[pos - 1] == ',';
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
        bool bitIsSet = false;
        
        if (pos >= 0)
        {
            // Check if the replica is at position 0 or preceded by comma
            if (pos == 0)
                bitIsSet = writeVector.Length == replica.Length || writeVector[replica.Length] == ',';
            else
                bitIsSet = writeVector[pos - 1] == ',';
        }
        
        if (bitIsSet)
        {
            // Bit is set, remove it
            var startPos = pos == 0 ? 0 : pos - 1; // Include comma if not at start
            var endPos = writeVector.IndexOf(',', pos + replica.Length);
            if (endPos == -1)
                endPos = writeVector.Length;
            else if (pos == 0)
                endPos = endPos + 1; // Remove the comma after replica when at start
                
            writeVector = writeVector.Remove(startPos, endPos - startPos);
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