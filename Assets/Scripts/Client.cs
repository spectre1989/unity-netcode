using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class Client : MonoBehaviour
{
    public Server Server;
    public GameObject[] PrefabTable;
    public float Latency;
    private List<NetObject> _objects;
    private List<byte[]> _snapshots;
    private float _snapshotLerpT;
    private Queue<PendingPacket> _pendingPackets;

    private void Start()
    {
        _objects = new List<NetObject>();
        _snapshots = new List<byte[]>();
        _pendingPackets = new Queue<PendingPacket>();
    }

    public void ReceivePacket(byte[] packet)
    {
        _pendingPackets.Enqueue(new PendingPacket { timeToConsume = Time.time + Latency, packet = packet });
    }

    private void Update()
    {
        while(_pendingPackets.Count > 0 && _pendingPackets.Peek().timeToConsume <= Time.time)
        {
            _snapshots.Add(_pendingPackets.Dequeue().packet);
        }

        if (_snapshots.Count > 1)
        {
            float snapshotDeltaTime = BitConverter.ToSingle(_snapshots[0], 0);

            _snapshotLerpT += (Time.deltaTime / snapshotDeltaTime);
            while (_snapshotLerpT >= 1.0f && _snapshots.Count > 1)
            {
                _snapshotLerpT -= 1.0f;
                _snapshots.RemoveAt(0);
            }

            if (_snapshots.Count > 1)
            {
                Interpolate(_snapshots[0], _snapshots[1], _snapshotLerpT);
            }
            else
            {
                Interpolate(_snapshots[0], _snapshots[0], 0.0f);
                _snapshotLerpT = 0.0f;
            }
        }

        byte packedInput = (byte)(
            (Input.GetKey(KeyCode.W) ? 1 : 0) | 
            (Input.GetKey(KeyCode.S) ? 2 : 0) |
            (Input.GetKey(KeyCode.A) ? 4 : 0) |
            (Input.GetKey(KeyCode.D) ? 8 : 0));
        byte[] packet = new byte[1500];
        packet[0] = packedInput;
        BitConverter.GetBytes(Time.deltaTime).CopyTo(packet, 1);

        Server.ReceivePacket(packet, Latency);
    }

    private void Interpolate(byte[] a, byte[] b, float t)
    {
        int readPos = 4;
        int objectCount = BitConverter.ToInt32(a, readPos);
        readPos += 4;

        for (int i = 0; i < objectCount; ++i)
        {
            int prefabId = BitConverter.ToInt32(a, readPos);
            readPos += 4;

            if (i == _objects.Count)
            {
                GameObject obj = Instantiate(PrefabTable[prefabId]);
                obj.transform.parent = this.transform;
                _objects.Add(obj.GetComponent<NetObject>()); // TODO check the NetObject component exists, warn accordingly if not
            }

            PropertyInfo[] properties = _objects[i].GetType().GetProperties(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance);

            for (int prop = 0; prop < properties.Length; ++prop)
            {
                foreach (CustomAttributeData customAttributeData in properties[prop].CustomAttributes)
                {
                    if (customAttributeData.AttributeType == typeof(NetSerialiseAttribute))
                    {
                        if (properties[prop].PropertyType == typeof(Vector3))
                        {
                            Vector3 from = new Vector3(
                                BitConverter.ToSingle(a, readPos),
                                BitConverter.ToSingle(a, readPos + 4),
                                BitConverter.ToSingle(a, readPos + 8));
                            Vector3 to = new Vector3(
                                BitConverter.ToSingle(b, readPos),
                                BitConverter.ToSingle(b, readPos + 4),
                                BitConverter.ToSingle(b, readPos + 8));
                            properties[prop].SetValue(_objects[i], Vector3.Lerp(from, to, t));
                            readPos += 12;
                        }
                        else if (properties[prop].PropertyType == typeof(float))
                        {
                            float from = BitConverter.ToSingle(a, readPos);
                            float to = BitConverter.ToSingle(b, readPos);
                            properties[prop].SetValue(_objects[i], Mathf.Lerp(from, to, t));
                            readPos += 4;
                        }
                        if (properties[prop].PropertyType == typeof(Quaternion))
                        {
                            Quaternion from = new Quaternion(
                                BitConverter.ToSingle(a, readPos),
                                BitConverter.ToSingle(a, readPos + 4),
                                BitConverter.ToSingle(a, readPos + 8),
                                BitConverter.ToSingle(a, readPos + 12));
                            Quaternion to = new Quaternion(
                                BitConverter.ToSingle(b, readPos),
                                BitConverter.ToSingle(b, readPos + 4),
                                BitConverter.ToSingle(b, readPos + 8),
                                BitConverter.ToSingle(b, readPos + 12));
                            properties[prop].SetValue(_objects[i], Quaternion.Slerp(from, to, t));
                            readPos += 16;
                        }

                        break;
                    }
                }
            }
        }
    }
}
