using System.Collections.Generic;
using System;
using UnityEngine;
using System.Reflection;

public class Server : MonoBehaviour
{
    public float Tickrate;
    public int SnapshotInterval;
    public Client Client;
    public GameObject[] PrefabTable;
    private List<NetObject> _objects;
    private List<int> _objectPrefabIds;
    private float _tickAccumulator;
    private int _snapshotAccumulator;

    private void Start()
    {
        _objects = new List<NetObject>();
        _objectPrefabIds = new List<int>();
    }

    private void OnGUI()
    {
        for (int i = 0; i < PrefabTable.Length; ++i)
        {
            if (GUILayout.Button(PrefabTable[i].name))
            {
                GameObject obj = Instantiate(PrefabTable[i]);
                obj.transform.parent = this.transform;
                obj.transform.localPosition = new Vector3(0.0f, 3.0f, 0.0f);

                _objects.Add(obj.GetComponent<NetObject>()); // TODO detect absence of NetObject script and log accordingly
                _objectPrefabIds.Add(i);
            }
        }
    }

    private void Update()
    {
        float tickDeltaTime = 1.0f / Tickrate;

        _tickAccumulator += Time.deltaTime;
        while (_tickAccumulator >= tickDeltaTime)
        {
            _tickAccumulator -= tickDeltaTime;

            Physics.Simulate(tickDeltaTime);

            ++_snapshotAccumulator;
            if (_snapshotAccumulator == SnapshotInterval)
            {
                _snapshotAccumulator = 0;

                byte[] packet = new byte[1500]; // TODO how to properly handle MTU

                int writePos = 0;
                float snapshotDeltaTime = tickDeltaTime * SnapshotInterval;
                BitConverter.GetBytes(snapshotDeltaTime).CopyTo(packet, writePos);
                writePos += 4;

                BitConverter.GetBytes(_objects.Count).CopyTo(packet, writePos);
                writePos += 4;

                for (int i = 0; i < _objects.Count; ++i)
                {
                    BitConverter.GetBytes(_objectPrefabIds[i]).CopyTo(packet, writePos);
                    writePos += 4;

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
                                    Vector3 v = (Vector3)properties[prop].GetValue(_objects[i]);
                                    BitConverter.GetBytes(v.x).CopyTo(packet, writePos);
                                    BitConverter.GetBytes(v.y).CopyTo(packet, writePos + 4);
                                    BitConverter.GetBytes(v.z).CopyTo(packet, writePos + 8);
                                    writePos += 12;
                                }
                                else if (properties[prop].PropertyType == typeof(float))
                                {
                                    float f = (float)properties[prop].GetValue(_objects[i]);
                                    BitConverter.GetBytes(f).CopyTo(packet, writePos);
                                    writePos += 4;
                                }
                                else if (properties[prop].PropertyType == typeof(Quaternion))
                                {
                                    Quaternion q = (Quaternion)properties[prop].GetValue(_objects[i]);
                                    BitConverter.GetBytes(q.x).CopyTo(packet, writePos);
                                    BitConverter.GetBytes(q.y).CopyTo(packet, writePos + 4);
                                    BitConverter.GetBytes(q.z).CopyTo(packet, writePos + 8);
                                    BitConverter.GetBytes(q.w).CopyTo(packet, writePos + 12);
                                    writePos += 16;
                                }

                                break;
                            }
                        }
                    }
                }

                Client.ReceivePacket(packet);
            }
        }
    }
}
