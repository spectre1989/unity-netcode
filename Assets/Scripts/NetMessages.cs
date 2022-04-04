using System;
using UnityEngine;

public struct ClientInputMsg
{
    public int tickId;
    public float dt;
    public NetPlayer.Input input;
}

public struct SnapshotHeaderMsg
{
    public float snapshotDeltaTime;
    public int clientTickId;
    public int objectCount;
}