using System;
using UnityEngine;

public class NetObject : MonoBehaviour
{
    public bool IsClient
    {
        get
        {
            return this.GetComponentInParent<Client>() != null;
        }
    }

    public bool IsServer
    {
        get
        {
            return this.GetComponentInParent<Server>() != null;
        }
    }
}

public class NetSerialiseAttribute : Attribute
{
}