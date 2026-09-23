using System;
using Unity.Netcode;


// Holds a player and all its required data
public struct CellData : INetworkSerializable, IEquatable<CellData>
{
    public bool IsBomb;
    public ulong RevealedBy;
    public int Points;
    public int Index;

    
    // Define how to serialize the object
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Points);
        serializer.SerializeValue(ref Index);
        serializer.SerializeValue(ref RevealedBy);
    }

    
    // Define how to match equality for 2 Cell objects
    // This defines when an OnChange is called
    public bool Equals(CellData other)
    {
        return Index == other.Index &&
               RevealedBy == other.RevealedBy &&
               Points == other.Points;
    }
}