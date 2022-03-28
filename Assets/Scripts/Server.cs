using System.Collections.Generic;
using System;
using UnityEngine;

public class Server : MonoBehaviour
{
    public Client Client;
    private List<GameObject> _objects;

    private void Start()
    {
        _objects = new List<GameObject>();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Create Object"))
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.transform.parent = this.transform;
            obj.transform.localPosition = new Vector3(0.0f, 3.0f, 0.0f);
            obj.AddComponent<Rigidbody>();

            _objects.Add(obj);
        }
    }

    private void Update()
    {
        byte[] packet = new byte[1500]; // TODO how to properly handle MTU

        int writePos = 0;
        BitConverter.GetBytes(_objects.Count).CopyTo(packet, writePos);
        writePos += 4;

        for (int i = 0; i < _objects.Count; ++i)
        {
            Transform t = _objects[i].transform;

            BitConverter.GetBytes(t.localPosition.x).CopyTo(packet, writePos);
            writePos += 4;
            BitConverter.GetBytes(t.localPosition.y).CopyTo(packet, writePos);
            writePos += 4;
            BitConverter.GetBytes(t.localPosition.z).CopyTo(packet, writePos);
            writePos += 4;

            BitConverter.GetBytes(t.localEulerAngles.x).CopyTo(packet, writePos);
            writePos += 4;
            BitConverter.GetBytes(t.localEulerAngles.y).CopyTo(packet, writePos);
            writePos += 4;
            BitConverter.GetBytes(t.localEulerAngles.z).CopyTo(packet, writePos);
            writePos += 4;
        }

        Client.ReceivePacket(packet);
    }
}
