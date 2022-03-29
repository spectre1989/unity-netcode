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
    public Vector3 Rot
    {
        get { return this.transform.localEulerAngles; }
        set { this.transform.localEulerAngles = value; }
    }
}
