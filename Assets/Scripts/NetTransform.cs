using System;
using UnityEngine;

public class NetTransform : NetObject
{
    [NetSerialise]
    public Vector3 Pos
    {
        get { return this.transform.localPosition; }
        set { this.transform.localPosition = value; }
    }

    [NetSerialise]
    public Quaternion Rot
    {
        get { return this.transform.localRotation; }
        set { this.transform.localRotation = value; }
    }
}
