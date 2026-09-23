using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum PlayableStates
{
    Playable = 0,
    Disqualified = 1,
}


// Holds a player and all its required data
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public int Score;
    public Color Color;
    public PlayableStates PlayableState;
    public FixedString64Bytes Name;

    
    // Define how to serialize the object
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayableState);
        serializer.SerializeValue(ref Score);
        serializer.SerializeValue(ref Color);
        serializer.SerializeValue(ref Name);
    }

    
    // Define how to match equality for 2 Player objects
    // This defines when an OnChange is called
    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId &&
               Score == other.Score &&
               Color == other.Color &&
               PlayableState == other.PlayableState &&
               Name == other.Name;
    }
}