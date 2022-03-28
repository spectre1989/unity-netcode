using System;
using System.Collections.Generic;
using UnityEngine;

public class Client : MonoBehaviour
{
    private List<GameObject> _objects;

    private void Start()
    {
        _objects = new List<GameObject>();
    }

    public void ReceivePacket(byte[] packet)
    {
        int readPos = 0;
        int objectCount = BitConverter.ToInt32(packet, readPos);
        readPos += 4;

        for (int i = 0; i < objectCount; ++i)
        {
            if (i == _objects.Count)
            {
                GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.parent = this.transform;
                _objects.Add(obj);
            }

            _objects[i].transform.localPosition = new Vector3(
                BitConverter.ToSingle(packet, readPos),
                BitConverter.ToSingle(packet, readPos + 4),
                BitConverter.ToSingle(packet, readPos + 8));
            readPos += 12;

            _objects[i].transform.localEulerAngles = new Vector3(
                BitConverter.ToSingle(packet, readPos),
                BitConverter.ToSingle(packet, readPos + 4),
                BitConverter.ToSingle(packet, readPos + 8));
            readPos += 12;
        }
    }
}
